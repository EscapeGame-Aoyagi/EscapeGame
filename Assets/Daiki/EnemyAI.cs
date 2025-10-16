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
    [SerializeField] private string paramWalk = "walk";
    [SerializeField] private string paramDash = "dash";

    [Header("Detection")]
    [SerializeField] private float viewDistance = 18f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField] private LayerMask obstacleMask = ~0; // 遮蔽物/地形レイヤーを想定
    [SerializeField] private float hearingRadius = 6f;
    [SerializeField] private int fovRayCount = 9;

    [Header("Vision Origin (★視点調整)")]
    [SerializeField, Tooltip("頭(目)のTransform。未指定時は自身の中心＋EyeHeight")]
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
    [SerializeField] private float lostGraceTime = 0.3f;
    [SerializeField] private Vector2 searchLookInterval = new(0.8f, 1.4f);
    [SerializeField] private float searchTurnSpeed = 180f;

    private State state = State.Patrol;
    private int wpIndex = 0;
    private float pauseTimer = 0f;
    private float searchTimer = 0f;
    private Vector3 lastSeenPos;
    private float lastSeenTime = -999f;

    // Search見渡し制御
    private bool isLookingAround = false; // 到達後の首振りフェーズ
    private float lookTimer = 0f;
    private Vector3 lookDir = Vector3.forward;

    void Reset() { agent = GetComponent<NavMeshAgent>(); }

    void Start()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!player) player = GameObject.FindGameObjectWithTag("Player")?.transform;

        GoToNextWaypoint();
        ChangeState(State.Patrol);
    }

    void Update()
    {
        if (!player) return;

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
                        FaceTarget(player.position);
                        // animator?.SetTrigger("attack");
                    }
                }
                else
                {
                    ChangeState(State.Search);
                }
                break;

            case State.Search:
                // ★到達までは移動（walkアニメ）、到達後は停止して見渡し（stayアニメ）
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
                        // まだ到達していない ⇒ 目的地へ移動継続
                        agent.isStopped = false; // 念のため
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

                // 再検知で追跡復帰
                if (Time.time - lastSeenTime <= 0f)
                {
                    ChangeState(State.Chase);
                }
                break;
        }

        EnforceAnimFlags(); // ★ここで常に正しいフラグに固定
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
        agent.stoppingDistance = 0.2f; // 少し余裕
        agent.SetDestination(waypoints[wpIndex].position);
        wpIndex = (wpIndex + 1) % waypoints.Length;
    }

    // ===== 検知 =====
    private bool CanSeePlayer()
    {
        Vector3 origin = eye ? eye.position : (transform.position + Vector3.up * eyeHeight); // ★視点を可変に
        Vector3 toPlayer = player.position - origin;
        float dist = toPlayer.magnitude;
        if (dist > viewDistance) return false;

        Vector3 dir = toPlayer.normalized;
        // 視野角（水平面基準）：目線の高さ差は考慮せずXZで判定する方が安定
        Vector3 fwdFlat = transform.forward; fwdFlat.y = 0f; fwdFlat.Normalize();
        Vector3 dirFlat = dir; dirFlat.y = 0f; dirFlat.Normalize();
        float angle = Vector3.Angle(fwdFlat, dirFlat);
        if (angle > viewAngle * 0.5f) return false;

        // ★最初に当たったのがPlayerかを確認（遮蔽物でブロック）
        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.CompareTag("Player")) return false;
        }
        // obstacleMaskにPlayerが含まれておらず最初に何も当たらない場合もあるので、追加で安全チェック
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
                agent.isStopped = false;               // まずは移動
                agent.speed = patrolSpeed * 1.1f;
                agent.stoppingDistance = 0.2f;
                agent.SetDestination(lastSeenPos);
                searchTimer = 0f;
                isLookingAround = false;               // 到達後に true へ
                break;
        }
    }

    // ===== Animatorフラグ（★Search移動中=walk、到達後=stay） =====
    private void EnforceAnimFlags()
    {
        if (!animator) return;
        bool wantWalk = false;
        bool wantDash = false;

        switch (state)
        {
            case State.Patrol:
                wantWalk = true;  // 巡回はwalk
                break;
            case State.Chase:
                wantDash = true;  // 追跡はdash
                break;
            case State.Search:
                // 到達前(=移動中) → walk、到達後(=見渡し) → 両方falseでstay
                bool movingToLast = !isLookingAround && !agent.isStopped && agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.01f && agent.velocity.sqrMagnitude > 0.01f;
                wantWalk = movingToLast;
                wantDash = false;
                break;
        }

        if (wantDash) wantWalk = false; // dash優先（保険）
        animator.SetBool(paramWalk, wantWalk);
        animator.SetBool(paramDash, wantDash);
    }

    // ===== 見失い中の見渡し（停止状態） =====
    private void ResetSearchLook()
    {
        lookTimer = 0f;
        lookDir = transform.forward;
        agent.updateRotation = false;   // 停止中は手動回頭
        agent.velocity = Vector3.zero;  // 念のため停止
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

    private void FaceTarget(Vector3 pos)
    {
        Vector3 flat = (pos - transform.position); flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(flat.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 720f * Time.deltaTime);
    }

    // ===== デバッグ可視化 =====
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, hearingRadius);
        Gizmos.color = Color.cyan;   Gizmos.DrawWireSphere(transform.position, viewDistance);

        // FOV可視化
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
