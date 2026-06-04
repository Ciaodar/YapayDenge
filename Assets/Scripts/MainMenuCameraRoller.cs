using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuCameraRoller : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Kameranın dönme hızı.")]
    public float rotationSpeed = 10f;
    
    [Tooltip("Kamera rastgele dönerken X ve Z eksenlerinde ne kadar eğilebileceği sınırı.")]
    public float tiltLimitX = 30f;
    public float tiltLimitZ = 15f;

    private Quaternion targetRotation;

    void Start()
    {
        SetNewRandomRotation();
    }

    void Update()
    {
        // Kamerayı yumuşak bir şekilde hedefe doğru döndür
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        // Hedef açıya çok yaklaşıldıysa yeni bir hedef belirle
        if (Quaternion.Angle(transform.rotation, targetRotation) < 1f)
        {
            SetNewRandomRotation();
        }
    }

    private void SetNewRandomRotation()
    {
        // Rastgele bir açı seç. Y ekseni tam tur dönebilir (0-360) 
        // Fakat X (Yukarı/Aşağı) ve Z (Sağ/Sol Yatma) eksenlerini limitliyoruz ki kamera baş aşağı dönmesin.
        float randomX = Random.Range(-tiltLimitX, tiltLimitX);
        float randomY = Random.Range(0f, 360f);
        float randomZ = Random.Range(-tiltLimitZ, tiltLimitZ);
        
        targetRotation = Quaternion.Euler(randomX, randomY, randomZ);
    }
}
