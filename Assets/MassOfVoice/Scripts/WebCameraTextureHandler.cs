using UnityEngine;

namespace MassOfVoice
{
    public class WebCameraTextureHandler : MonoBehaviour
    {
        [SerializeField] int width = 1920;
        [SerializeField] int height = 1080;
        [SerializeField] int fps = 30;

        WebCamTexture webcamTexture;

        void Start()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            webcamTexture = new WebCamTexture(devices[0].name, width, height, fps);
            GetComponent<Renderer>().material.mainTexture = webcamTexture;
            webcamTexture.Play();
        }
    }
}