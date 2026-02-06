using UnityEngine;
using Fusion;

public class VRPlayerMovement : NetworkBehaviour
{
    [SerializeField] private float launchPower = 10.0f;
    [SerializeField] private float friction = 0.98f;
    [SerializeField] private float stopThreshold = 0.01f;

    [SerializeField][Range(0f, 1f)] private float collisionSpeedRetention = 0.2f;

    [SerializeField] private float normalMoveSpeed = 2.0f;

    private Transform cameraRigRoot;
    private Transform centerEyeAnchor;
    private Vector3 currentVelocity;

    private bool isLaunchRequested = false;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            var rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null)
            {
                cameraRigRoot = rig.transform;
                centerEyeAnchor = rig.centerEyeAnchor;
            }
            else
            {
            }
        }
    }

    private void Update()
    {
        if (!HasStateAuthority || cameraRigRoot == null) return;

        Vector2 leftInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

        if (leftInput.magnitude > 0.1f && OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            isLaunchRequested = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || cameraRigRoot == null || centerEyeAnchor == null)
        {
            return;
        }

        if (currentVelocity.magnitude > stopThreshold)
        {
            cameraRigRoot.position += currentVelocity * Runner.DeltaTime;
            currentVelocity *= friction;
        }
        else
        {
            currentVelocity = Vector3.zero;
        }

        Vector3 forward = centerEyeAnchor.forward;
        Vector3 right = centerEyeAnchor.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        if (isLaunchRequested)
        {
            Vector2 leftInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

            if (leftInput.magnitude > 0.1f)
            {
                Vector3 launchDir = (forward * leftInput.y + right * leftInput.x).normalized;
                currentVelocity = launchDir * launchPower;
            }

            isLaunchRequested = false;
        }

        Vector2 rightInput = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
        if (rightInput.magnitude > 0.1f)
        {
            Vector3 moveDirection = (forward * rightInput.y + right * rightInput.x);
            cameraRigRoot.position += moveDirection * normalMoveSpeed * Runner.DeltaTime;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!HasStateAuthority) return;

        var ball = collision.gameObject.GetComponent<BilliardBall>();
        if (ball != null)
        {
            Vector3 dir = (ball.transform.position - transform.position).normalized;
            dir.y = 0;
            float power = Mathf.Max(currentVelocity.magnitude, 1.0f);

            Vector3 newVelocity = dir * power * 1.2f;

            ball.OnHit(newVelocity, Runner.LocalPlayer);

            currentVelocity *= collisionSpeedRetention;

        }
        else if (collision.gameObject.CompareTag("Wall"))
        {
            Vector3 normal = collision.contacts[0].normal;
            currentVelocity = Vector3.Reflect(currentVelocity, normal);
            currentVelocity *= 0.8f;
        }
    }
}