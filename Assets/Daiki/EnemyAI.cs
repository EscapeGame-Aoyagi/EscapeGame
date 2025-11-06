using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Search }

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private Animator animator;

    [Header("Animator Param Names (bool)")]
    [SerializeField] private string paramWalk = "walk"; // trueでwalk
    [SerializeField] private string paramDash = "dash"; // trueでdash

    [Header("Detection")]
    [SerializeField] private float viewDistance = 18f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private float hearingRadius = 6f;
    [SerializeField] private int fovRayCount = 9;

    [Header("Vision Origin")]
    [SerializeField, Tooltip("頭(目)のTransform。未指定時は自身の中心＋eyeHeight")]
    private Transform eye;
    [SerializeField, Tooltip("eye未指定時の発射高さ")]
    private float eyeHeight = 1.2f;

    [Header("Combat / Movement")]
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float patrolSpeed = 2.0f;
    [SerializeField] private float chaseSpeed = 4.0f;
    [SerializeField] private float searchDuration = 4.0f;
    [SerializeField] private float waypointPause = 0.5f;

    [Header("Stability Tweaks")]
    [SerializeField, Tooltip("見え隠れを許す時間")]
    private float lostGraceTime = 0.3f;
    [SerializeField, Tooltip("見渡しの首振り間隔")]
    private Vector2 searchLookInterval = new(0.8f, 1.4f);
    [SerializeField, Tooltip("見渡し中の回頭速度")]
    private float searchTurnSpeed = 180f;

    [Header("SFX")]
    [SerializeField, Tooltip("捕まえた瞬間に鳴らすAudioSource")]
    private AudioSource audioSource;
    [SerializeField, Tooltip("捕まえた瞬間のSE")]
    private AudioClip caughtSfx;

    // 内部状態
    private State state = State.Patrol;
    private int wpIndex = 0;
    private float pauseTimer = 0f;
    private float searchTimer = 0f;
    private Vector3 lastSeenPos;
    private float lastSeenTime = -999f;

    // Search見渡し
    private bool isLookingAround = false;
    private float lookTimer = 0f;
    private Vector3 lookDir = Vector3.forward;

    // 捕獲中
    private bool isCapturingPlayer = false;
    private Vector3 capturedPos;
    private Quaternion capturedRot;
    private bool prevApplyRootMotion = false;

    // 捕獲中にプレイヤー側が敵の向きを回しているかどうか
    // trueの間は敵は回転ロックしない
    private bool captureRotationUnlocked = false;

    void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!player) player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (!audioSource) audioSource = GetComponent<AudioSource>();

        GoToNextWaypoint();
        ChangeState(State.Patrol);
    }

    void Update()
    {
        if (!player) return;

        // 捕獲演出中は基本は止める
        if (isCapturingPlayer)
        {
            if (agent)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            // 位置は固定
            transform.position = capturedPos;

            // 回転はプレイヤーがまだ合わせている間はロックしない
            if (!captureRotationUnlocked)
            {
                transform.rotation = capturedRot;
            }

            return;
        }

        bool canSee = CanSeePlayer();
        bool canHear = CanHearPlayer();

        if (canSee || canHear)
        {
            lastSeenPos = player.position;
            lastSeenTime = Time.time;
        }

        switch (state)
        {
            case State.Patrol:
                if (Time.time - lastSeenTime <= 0f)
                {
                    ChangeState(State.Chase);
                    break;
                }
                PatrolTick();
                break;

            case State.Chase:
                if (Time.time - lastSeenTime <= lostGraceTime)
                {
                    agent.isStopped = false;
                    agent.SetDestination(lastSeenPos);

                    float dist = Vector3.Distance(transform.position, player.position);
                    if (dist <= attackRange)
                    {
                        CapturePlayer(); // ★ここで捕まえる
                    }
                }
                else
                {
                    ChangeState(State.Search);
                }
                break;

            case State.Search:
                if (!isLookingAround)
                {
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
                    {
                        agent.isStopped = true;
                        agent.ResetPath();
                        isLookingAround = true;
                        ResetSearchLook();
                    }
                    else
                    {
                        agent.isStopped = false;
                    }
                }

                if (isLookingAround)
                {
                    searchTimer += Time.deltaTime;
                    SearchLookTick();
                    if (searchTimer >= searchDuration)
                    {
                        ChangeState(State.Patrol);
                    }
                }

                // また見えたら追跡へ
                if (Time.time - lastSeenTime <= 0f)
                {
                    ChangeState(State.Chase);
                }
                break;
        }

        EnforceAnimFlags();
    }

    // ===== 捕獲処理 =====
    private void CapturePlayer()
    {
        isCapturingPlayer = true;

        // ★SE再生
        if (audioSource && caughtSfx)
        {
            audioSource.PlayOneShot(caughtSfx);
        }

        // NavMeshを完全停止
        if (agent)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.velocity = Vector3.zero;
        }

        // RootMotionも止める
        if (animator)
        {
            prevApplyRootMotion = animator.applyRootMotion;
            animator.applyRootMotion = false;
            animator.SetBool(paramDash, false);
            animator.SetBool(paramWalk, false);
        }

        // 現在位置/回転を記録
        capturedPos = transform.position;
        capturedRot = transform.rotation;

        // この後プレイヤー側が回転させるので、いったんロックを外しておく
        captureRotationUnlocked = true;

        // プレイヤー側に通知
        var caught = player.GetComponent<PlayerCaughtHandler>();
        if (caught != null)
        {
            caught.StartCaughtSequence(this);
        }
        else
        {
            Debug.LogWarning("PlayerCaughtHandler がプレイヤーに付いていません");
        }
    }

    // プレイヤー側の演出から呼ばれる：歩きアニメだけ再生したいとき
    public void PlayWalkAfterCapture()
    {
        if (animator)
        {
            animator.SetBool(paramDash, false);
            animator.SetBool(paramWalk, true);
        }
    }

    // プレイヤー側が「この向きにして」と渡してくる用
    public void SetCapturedRotation(Quaternion rot)
    {
        transform.rotation = rot;
    }

    // プレイヤー側が「もう回し終わった」と言ってくる用
    public void LockCapturedRotation()
    {
        captureRotationUnlocked = false;
        capturedRot = transform.rotation;
    }

    // ===== 巡回 =====
    private void PatrolTick()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
        {
            pauseTimer += Time.deltaTime;
            if (pauseTimer >= waypointPause)
            {
                pauseTimer = 0f;
                GoToNextWaypoint();
            }
        }
    }

    private void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        agent.isStopped = false;
        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0.2f;
        agent.SetDestination(waypoints[wpIndex].position);
        wpIndex = (wpIndex + 1) % waypoints.Length;
    }

    // ===== 検知 =====
    private bool CanSeePlayer()
    {
        Vector3 origin = eye ? eye.position : (transform.position + Vector3.up * eyeHeight);
        Vector3 toPlayer = player.position - origin;
        float dist = toPlayer.magnitude;
        if (dist > viewDistance) return false;

        Vector3 dir = toPlayer.normalized;
        // 水平で視野角チェック
        Vector3 fwdFlat = transform.forward; fwdFlat.y = 0f; fwdFlat.Normalize();
        Vector3 dirFlat = dir; dirFlat.y = 0f; dirFlat.Normalize();
        float angle = Vector3.Angle(fwdFlat, dirFlat);
        if (angle > viewAngle * 0.5f) return false;

        // 遮蔽物チェック
        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.CompareTag("Player")) return false;
        }
        // 保険: 全レイヤーで
        if (Physics.Raycast(origin, dir, out RaycastHit hitAll, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hitAll.transform.CompareTag("Player")) return false;
        }

        return true;
    }

    private bool CanHearPlayer()
    {
        return Vector3.Distance(transform.position, player.position) <= hearingRadius;
    }

    // ===== 状態遷移 =====
    private void ChangeState(State next)
    {
        state = next;
        switch (next)
        {
            case State.Patrol:
                agent.updateRotation = true;
                agent.isStopped = false;
                searchTimer = 0f;
                isLookingAround = false;
                GoToNextWaypoint();
                agent.speed = patrolSpeed;
                agent.stoppingDistance = 0.2f;
                break;

            case State.Chase:
                agent.updateRotation = true;
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                agent.stoppingDistance = attackRange * 0.9f;
                agent.SetDestination(lastSeenPos);
                isLookingAround = false;
                break;

            case State.Search:
                agent.updateRotation = true;
                agent.isStopped = false;
                agent.speed = patrolSpeed * 1.1f;
                agent.stoppingDistance = 0.2f;
                agent.SetDestination(lastSeenPos);
                searchTimer = 0f;
                isLookingAround = false;
                break;
        }
    }

    // ===== Animatorフラグ制御 =====
    private void EnforceAnimFlags()
    {
        if (!animator) return;

        // 捕獲中はここ触らない（演出で指定する）
        if (isCapturingPlayer) return;

        bool wantWalk = false;
        bool wantDash = false;

        switch (state)
        {
            case State.Patrol:
                wantWalk = true;
                break;
            case State.Chase:
                wantDash = true;
                break;
            case State.Search:
                bool movingToLast =
                    !isLookingAround && !agent.isStopped && agent.hasPath &&
                    agent.remainingDistance > agent.stoppingDistance + 0.01f &&
                    agent.velocity.sqrMagnitude > 0.01f;
                wantWalk = movingToLast;
                break;
        }

        if (wantDash) wantWalk = false;
        animator.SetBool(paramWalk, wantWalk);
        animator.SetBool(paramDash, wantDash);
    }

    // ===== 見失い中の見渡し =====
    private void ResetSearchLook()
    {
        lookTimer = 0f;
        lookDir = transform.forward;
        agent.updateRotation = false;
        agent.velocity = Vector3.zero;
    }

    private void SearchLookTick()
    {
        lookTimer -= Time.deltaTime;
        if (lookTimer <= 0f)
        {
            Vector2 r = Random.insideUnitCircle.normalized;
            Vector3 targetDir = new Vector3(r.x, 0f, r.y);

            Vector3 toLast = lastSeenPos - transform.position; toLast.y = 0f;
            if (toLast.sqrMagnitude > 0.01f)
                targetDir = (targetDir * 0.4f + toLast.normalized * 0.6f).normalized;

            lookDir = targetDir;
            lookTimer = Random.Range(searchLookInterval.x, searchLookInterval.y);
        }

        Quaternion look = Quaternion.LookRotation(lookDir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, searchTurnSpeed * Time.deltaTime);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, hearingRadius);
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, viewDistance);

        if (fovRayCount > 1)
        {
            float half = viewAngle * 0.5f;
            for (int i = 0; i < fovRayCount; i++)
            {
                float t = (fovRayCount == 1) ? 0.5f : (float)i / (fovRayCount - 1);
                float ang = Mathf.Lerp(-half, half, t);
                Quaternion rot = Quaternion.AngleAxis(ang, Vector3.up);
                Vector3 dir = rot * transform.forward;
                Vector3 org = eye ? eye.position : (transform.position + Vector3.up * eyeHeight);
                Gizmos.DrawRay(org, dir * Mathf.Min(viewDistance, 3f));
            }
        }
    }
}
