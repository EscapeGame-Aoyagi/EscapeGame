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
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float hearingRadius = 6f;
    [SerializeField] private int fovRayCount = 9;

    [Header("Combat / Movement")]
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float patrolSpeed = 2.0f;
    [SerializeField] private float chaseSpeed = 4.0f;
    [SerializeField] private float searchDuration = 4.0f;
    [SerializeField] private float waypointPause = 0.5f;

    [Header("Stability Tweaks")]
    [SerializeField, Tooltip("検知が切れてからSearchへ落とすまでの猶予")]
    private float lostGraceTime = 0.3f;
    [SerializeField, Tooltip("見失い中、視線の更新間隔（秒）")]
    private Vector2 searchLookInterval = new Vector2(0.8f, 1.4f);
    [SerializeField, Tooltip("見失い中の回頭速度（deg/sec）")]
    private float searchTurnSpeed = 180f;

    private State state = State.Patrol;
    private int wpIndex = 0;
    private float pauseTimer = 0f;
    private float searchTimer = 0f;
    private Vector3 lastSeenPos;

    // ちらつき対策
    private float lastSeenTime = -999f;

    // 見失い中のスムーズ首振り
    private float lookTimer = 0f;
    private Vector3 lookDir = Vector3.forward;

    void Reset() { agent = GetComponent<NavMeshAgent>(); }

    void Start()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!player) player = GameObject.FindGameObjectWithTag("Player")?.transform;

        GoToNextWaypoint();
        ChangeState(State.Patrol); // 状態適用
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
                // 追跡への遷移
                if (Time.time - lastSeenTime <= 0f)   // 今フレームで検知
                {
                    ChangeState(State.Chase);
                    break;
                }
                PatrolTick();
                break;

            case State.Chase:
                if (Time.time - lastSeenTime <= lostGraceTime)
                {
                    // 追跡継続（微妙に見え隠れしても落とさない）
                    agent.SetDestination(lastSeenPos);

                    float dist = Vector3.Distance(transform.position, player.position);
                    if (dist <= attackRange)
                    {
                        FaceTarget(player.position);
                        // 攻撃トリガーがあればここで
                        // animator?.SetTrigger("attack");
                    }
                }
                else
                {
                    // 猶予切れでSearchへ
                    ChangeState(State.Search);
                }
                break;

            case State.Search:
                // 最後に見た位置まで移動
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
                {
                    searchTimer += Time.deltaTime;
                    SearchLookTick(); // スムーズに周囲を見回す
                    if (searchTimer >= searchDuration)
                    {
                        ChangeState(State.Patrol);
                    }
                }
                else
                {
                    // 走行中はNavMeshの回頭に任せる
                    if (!agent.updateRotation) agent.updateRotation = true;
                }

                // 再検知で追跡復帰
                if (Time.time - lastSeenTime <= 0f)
                {
                    ChangeState(State.Chase);
                }
                break;
        }

        // ★Animatorのちらつき対策：毎フレーム「意図したフラグ」を上書き★
        EnforceAnimFlags();
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
        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0.1f;
        agent.SetDestination(waypoints[wpIndex].position);
        wpIndex = (wpIndex + 1) % waypoints.Length;
    }

    // ===== 検知 =====
    private bool CanSeePlayer()
    {
        Vector3 toPlayer = (player.position - transform.position);
        float dist = toPlayer.magnitude;
        if (dist > viewDistance) return false;

        Vector3 dir = toPlayer.normalized;
        float angle = Vector3.Angle(transform.forward, dir);
        if (angle > viewAngle * 0.5f) return false;

        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, dir, out RaycastHit hit, viewDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.CompareTag("Player")) return true;
        }
        return false;
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
                agent.updateRotation = true;               // NavMeshに回頭を任せる
                searchTimer = 0f;
                GoToNextWaypoint();
                agent.speed = patrolSpeed;
                agent.stoppingDistance = 0.1f;
                break;

            case State.Chase:
                agent.updateRotation = true;
                agent.speed = chaseSpeed;
                agent.stoppingDistance = attackRange * 0.9f;
                agent.SetDestination(lastSeenPos);
                break;

            case State.Search:
                // 目的地へ向かうまではNavMeshに回頭を任せ、
                // 到達後の首振り中だけ手動回頭に切り替える
                agent.updateRotation = true;
                agent.speed = patrolSpeed * 1.1f;
                agent.stoppingDistance = 0.1f;
                agent.SetDestination(lastSeenPos);
                searchTimer = 0f;
                ResetSearchLook();
                break;
        }
    }

    // ===== Animatorを毎フレーム強制 =====
    private void EnforceAnimFlags()
    {
        if (!animator) return;
        bool wantWalk = (state == State.Patrol);
        bool wantDash = (state == State.Chase);

        // dash優先。万一両方trueならdashのみtrueに矯正
        if (wantDash) wantWalk = false;

        animator.SetBool(paramWalk, wantWalk);
        animator.SetBool(paramDash, wantDash);
        // 両方falseの時だけ自然にstayへ
    }

    // ===== 見失い中のスムーズ見回し =====
    private void ResetSearchLook()
    {
        lookTimer = 0f;
        lookDir = transform.forward;
    }

    private void SearchLookTick()
    {
        // 到達後、首振りへ切替
        if (agent.updateRotation) agent.updateRotation = false;

        lookTimer -= Time.deltaTime;
        if (lookTimer <= 0f)
        {
            // 新しい注視方向を同じ水平面上でランダム選択（滑らかに向く）
            Vector3 rand = Random.insideUnitCircle.normalized;
            Vector3 targetDir = new Vector3(rand.x, 0f, rand.y);
            // 少し「最後に見た方向」を混ぜると自然
            Vector3 toLast = (lastSeenPos - transform.position);
            toLast.y = 0f;
            if (toLast.sqrMagnitude > 0.01f)
                targetDir = (targetDir * 0.4f + toLast.normalized * 0.6f).normalized;

            lookDir = targetDir;
            lookTimer = Random.Range(searchLookInterval.x, searchLookInterval.y);
        }

        // Slerpでなめらか回頭
        Quaternion look = Quaternion.LookRotation(lookDir);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, look,
            searchTurnSpeed * Time.deltaTime
        );
    }

    private void FaceTarget(Vector3 pos)
    {
        Vector3 flat = (pos - transform.position); flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(flat.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 720f * Time.deltaTime);
    }

    // ===== デバッグ可視化（省略可） =====
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, hearingRadius);
        Gizmos.color = Color.cyan;   Gizmos.DrawWireSphere(transform.position, viewDistance);

        if (fovRayCount > 1)
        {
            float half = viewAngle * 0.5f;
            for (int i = 0; i < fovRayCount; i++)
            {
                float t = (fovRayCount == 1) ? 0.5f : (float)i / (fovRayCount - 1);
                float ang = Mathf.Lerp(-half, half, t);
                Quaternion rot = Quaternion.AngleAxis(ang, Vector3.up);
                Vector3 dir = rot * transform.forward;
                Gizmos.DrawRay(transform.position + Vector3.up * 1.6f, dir * Mathf.Min(viewDistance, 3f));
            }
        }
    }
}
