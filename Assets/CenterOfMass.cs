using UnityEngine;

public class CenterOfMass : MonoBehaviour
{
   void Start()
   {
       // Pastikan objek ini adalah child dari boat
       if (transform.parent != null && transform.parent.GetComponent<Rigidbody>() != null)
       {
           // Set center of mass boat ke posisi lokal objek ini
           Rigidbody parentRb = transform.parent.GetComponent<Rigidbody>();
           parentRb.centerOfMass = transform.localPosition;
           
           // Buat visual marker
           GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
           marker.name = "COM_Marker";
           marker.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
           marker.GetComponent<Collider>().enabled = false;
           marker.transform.position = transform.position;
           
           // Optional: Beri warna berbeda
           Renderer markerRenderer = marker.GetComponent<Renderer>();
           if (markerRenderer != null)
           {
               markerRenderer.material.color = Color.red;
           }
           
           Debug.Log("Center of Mass set to: " + transform.localPosition);
       }
       else
       {
           Debug.LogError("CenterOfMass script harus ditempatkan pada child object dari objek dengan Rigidbody!");
       }
   }
}