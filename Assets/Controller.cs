using UnityEngine;

public class Controller : MonoBehaviour
{
    float inputX, inputZ;
    [SerializeField] float moveSpeed = 3.5f;
    [SerializeField] Transform cam, lig;

    float yaw;   // 水平回転（左右）
    float pitch; // 垂直回転（上下）
    [SerializeField] float sensitivityX = 120f; // deg/sec（水平）
    [SerializeField] float sensitivityY = 120f; // deg/sec（垂直）
    [SerializeField] float minPitch = -90f, maxPitch = 90f;

    bool cursorLock = true;

    void Start()
    {
        if (cam == null) Debug.LogError("cam未割り当て");
        if (lig == null) Debug.LogWarning("lig未割り当て");

        var rot = transform.localRotation.eulerAngles;
        yaw = rot.y;
        pitch = cam != null ? cam.localRotation.eulerAngles.x : 0f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // --- 視点回転（フレーム非依存） ---
        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y");

        yaw   += mx * sensitivityX * Time.deltaTime;
        pitch -= my * sensitivityY * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        if (cam != null)
        {
            cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        if (lig != null)
        {
            lig.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // --- 入力はUpdateで読む ---
        inputX = Input.GetAxisRaw("Horizontal");
        inputZ = Input.GetAxisRaw("Vertical");

        UpdateCursorLock();
    }

    void FixedUpdate()
    {
        // --- 水平面に投影して移動（フレーム非依存） ---
        if (cam == null) return;

        Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;

        Vector3 vel = (fwd * inputZ + right * inputX).normalized * moveSpeed;
        transform.position += vel * Time.fixedDeltaTime; // ※壁抜け対策はCharacterController推奨
    }

    void UpdateCursorLock()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) cursorLock = false;
        else if (Input.GetMouseButton(0))     cursorLock = true;

        if (cursorLock)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
