using UnityEngine;

namespace Rendering
{
    public class FreeFlyCamera : MonoBehaviour
    {
        [SerializeField] float moveSpeed        = 60f;
        [SerializeField] float fastMultiplier   = 3f;
        [SerializeField] float lookSensitivity  = 2f;
        [SerializeField] float scrollSensitivity = 15f;

        float _yaw;
        float _pitch;

        void Start()
        {
            _yaw   = transform.eulerAngles.y;
            _pitch = transform.eulerAngles.x;
        }

        void Update()
        {
            if (Input.GetMouseButton(1))
            {
                _yaw   += Input.GetAxis("Mouse X") * lookSensitivity;
                _pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
                _pitch  = Mathf.Clamp(_pitch, -90f, 90f);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            moveSpeed += Input.GetAxis("Mouse ScrollWheel") * scrollSensitivity;
            moveSpeed  = Mathf.Max(1f, moveSpeed);

            float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += transform.forward;
            if (Input.GetKey(KeyCode.S)) move -= transform.forward;
            if (Input.GetKey(KeyCode.A)) move -= transform.right;
            if (Input.GetKey(KeyCode.D)) move += transform.right;
            if (Input.GetKey(KeyCode.E)) move += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;

            if (move.sqrMagnitude > 0f)
                transform.position += move.normalized * speed * Time.deltaTime;
        }
    }
}
