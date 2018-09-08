using System.Collections;
using UnityEngine;

namespace MassOfVoice
{
    public class TextObjectController : MonoBehaviour
    {
        [SerializeField, Range(0.001f, 0.01f)] float alphaSubtraction = 0.005f;

        Material material;

        public void Initialize()
        {
            material = GetComponent<Renderer>().material;
            material.color = new Color(Random.Range(0.1f, 0.5f), 0.1f, Random.Range(0.5f, 0.9f), 1f);

            var rigidBody = gameObject.GetComponent<Rigidbody>();
            rigidBody.AddTorque(new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f)) * 10.0f);
            transform.position = new Vector3(transform.position.x * 1.5f, Random.Range(-2f, 2f), 0f);
            GetComponent<Renderer>().enabled = true;

            StartCoroutine(Disappear());
        }

        IEnumerator Disappear()
        {
            var colorSubtraction = new Color(0, 0, 0, alphaSubtraction);

            yield return new WaitForSeconds(1f);
            while(material.color.a > 0)
            {
                material.color -= colorSubtraction;
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}