using UnityEngine;

namespace KeystoneKapers
{
    /// <summary>Maleta ou saco de dinheiro opcional (50 pontos). Feito com primitivos.</summary>
    public class Collectible : MonoBehaviour
    {
        public float spinSpeed = 120f;
        public float bobAmplitude = 0.08f;
        public int Floor { get; private set; }
        public bool Active { get; private set; }
        Vector3 basePos;
        float t;

        public void Place(int floor, Vector3 pos)
        {
            Floor = floor;
            basePos = pos;
            transform.position = pos;
            Active = true;
            gameObject.SetActive(true);
        }

        public void Collect()
        {
            Active = false;
            gameObject.SetActive(false);
        }

        void Update()
        {
            t += Time.deltaTime;
            transform.position = basePos + Vector3.up * Mathf.Sin(t * 3f) * bobAmplitude;
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }

        public bool Overlaps(Bounds b)
        {
            var p = transform.position;
            return Mathf.Abs(p.x - b.center.x) < 0.55f && p.y > b.min.y - 0.2f && p.y < b.max.y + 0.2f;
        }
    }
}
