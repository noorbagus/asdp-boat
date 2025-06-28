using UnityEngine;

public class CameraFollow : MonoBehaviour
{
   [Header("Target Settings")]
   [SerializeField] private Transform target;
   [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -7f); // Lebih tinggi
   
   [Header("Follow Settings")]
   [SerializeField] private float smoothSpeed = 3f;
   [SerializeField] private float rotationSpeed = 2f;
   
   [Header("Camera Facing")]
   [SerializeField] private bool faceFront = true;
   [SerializeField] private KeyCode toggleViewKey = KeyCode.C;
   
   [Header("Look Settings")]
   [SerializeField] private bool lookAtHorizon = true;
   [SerializeField] private float lookUpOffset = 2f; // Positif = lihat ke atas
   
   [Header("Rotation Axis")]
   [SerializeField] private bool useXRotation = false;
   [SerializeField] private bool useYRotation = true;
   [SerializeField] private bool useZRotation = false;
   [SerializeField] private bool invertRotation = false;
   
   private float initialYRotation;
   private bool hasInitialRotation = false;
   
   private void Start()
   {
       if (target != null && !hasInitialRotation)
       {
           initialYRotation = target.eulerAngles.y;
           hasInitialRotation = true;
       }
   }
   
   private void Update()
   {
       // Toggle camera view with input
       if (Input.GetKeyDown(toggleViewKey))
       {
           faceFront = !faceFront;
       }
   }
   
   private void LateUpdate()
   {
       if (target == null) return;
       
       // Ambil posisi boat
       Vector3 boatPosition = target.position;
       
       // Ambil rotasi dasar
       float currentRotation = 0f;
       if (useXRotation) currentRotation = target.eulerAngles.x;
       else if (useYRotation) currentRotation = target.eulerAngles.y;
       else if (useZRotation) currentRotation = target.eulerAngles.z;
       
       // Invert rotation jika dicentang
       if (invertRotation) currentRotation = -currentRotation;
       
       // Tambahkan 180 derajat jika kamera menghadap ke depan
       if (faceFront && useYRotation)
       {
           currentRotation += 180f;
       }
       
       // Hitung posisi camera berdasarkan offset dan rotasi
       Vector3 rotatedOffset = Vector3.zero;
       Quaternion targetRotation = Quaternion.identity;
       
       if (useXRotation)
       {
           rotatedOffset = Quaternion.Euler(currentRotation, 0, 0) * offset;
           targetRotation = Quaternion.Euler(currentRotation, 0, 0);
       }
       else if (useYRotation)
       {
           rotatedOffset = Quaternion.Euler(0, currentRotation, 0) * offset;
           targetRotation = Quaternion.Euler(0, currentRotation, 0);
       }
       else if (useZRotation)
       {
           rotatedOffset = Quaternion.Euler(0, 0, currentRotation) * offset;
           targetRotation = Quaternion.Euler(0, 0, currentRotation);
       }
       else
       {
           rotatedOffset = offset;
           targetRotation = transform.rotation;
       }
       
       Vector3 desiredPosition = boatPosition + rotatedOffset;
       
       // Smooth position following
       transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
       
       // Kalkulasi target untuk look at
       if (lookAtHorizon)
       {
           // Target titik untuk melihat ke horizon
           Vector3 lookTarget = target.position;
           
           // Gunakan forward direction untuk menentukan arah horizon
           Vector3 forwardDir = faceFront ? -target.forward : target.forward;
           
           // Jarak pandang ke depan (biar tidak melihat ke boat)
           lookTarget += forwardDir * 20f;
           
           // Tambahkan offset vertikal (positif untuk melihat ke atas)
           lookTarget.y += lookUpOffset;
           
           // Arahkan kamera ke horizon
           transform.LookAt(lookTarget);
       }
       else
       {
           // Smooth rotation - gunakan cara original
           transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
           
           // Pastikan kamera selalu melihat ke target
           transform.LookAt(target);
       }
   }
}