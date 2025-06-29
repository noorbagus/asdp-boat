using UnityEngine;

public class HandTargetController : MonoBehaviour
{
    public Transform leftHandTarget;
    public Transform rightHandTarget;
    public BoatController boatController;
    
    [Header("Rotation Settings")]
    public Vector3 leftHandBaseRotation = new Vector3(0, 0, 0);
    public Vector3 rightHandBaseRotation = new Vector3(0, 0, 0);
    public float rotationSpeed = 5f;
    
    void Update()
    {
        // Contoh: Rotasi target berdasarkan status paddling
        if (boatController.IsLeftPaddling())
        {
            // Rotasi target tangan kiri saat dayung kiri aktif
            Vector3 targetRotation = leftHandBaseRotation + new Vector3(30, 0, 0);
            leftHandTarget.localRotation = Quaternion.Slerp(
                leftHandTarget.localRotation,
                Quaternion.Euler(targetRotation),
                Time.deltaTime * rotationSpeed
            );
        }
        else if (boatController.IsRightPaddling())
        {
            // Rotasi target tangan kanan saat dayung kanan aktif
            Vector3 targetRotation = rightHandBaseRotation + new Vector3(0, 0, 30);
            rightHandTarget.localRotation = Quaternion.Slerp(
                rightHandTarget.localRotation,
                Quaternion.Euler(targetRotation),
                Time.deltaTime * rotationSpeed
            );
        }
        else
        {
            // Kembali ke posisi normal
            leftHandTarget.localRotation = Quaternion.Slerp(
                leftHandTarget.localRotation,
                Quaternion.Euler(leftHandBaseRotation),
                Time.deltaTime * rotationSpeed
            );
            
            rightHandTarget.localRotation = Quaternion.Slerp(
                rightHandTarget.localRotation,
                Quaternion.Euler(rightHandBaseRotation),
                Time.deltaTime * rotationSpeed
            );
        }
    }
}