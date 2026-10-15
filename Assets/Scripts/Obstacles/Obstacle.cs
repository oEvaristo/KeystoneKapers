using UnityEngine;

namespace KeystoneKapers
{
    /// <summary>
    /// Obstáculo arcade com movimento previsível (sem física aleatória).
    /// Carrinho rola no chão, bola quica, avião voa na altura do policial em pé e o rádio fica parado.
    /// O collider original do prefab é usado como volume de impacto (marcado como trigger para não empurrar o jogador).
    /// </summary>
    public class Obstacle : MonoBehaviour
    {
        public ObstacleType type;
        [Tooltip("Collider do prefab original usado como volume de impacto.")]
        public Collider hitCollider;
        [Tooltip("Pivô visual (gira para a direção do movimento).")]
        public Transform visual;
        [Tooltip("Hélice do avião (opcional).")]
        public Transform propeller;
        [Range(0.5f, 1f)] public float hitShrink = 0.85f;
        [Tooltip("Altura da base do avião em relação ao piso.")]
        public float flightAltitude = 1.15f;
        [Tooltip("Raio visual (bola) para girar.")]
        public float rollRadius = 0.27f;

        public int Floor { get; private set; }
        public bool Active { get; private set; }
        public float Direction { get; private set; }

        float speed, bounceHeight, bouncePeriod, t, baseY;

        public void Launch(int floor, float x, float direction, RoundDifficulty d)
        {
            var layout = LevelLayout.Instance;
            Floor = floor;
            Direction = Mathf.Sign(direction);
            t = 0f;
            baseY = layout.FloorY(floor);
            switch (type)
            {
                case ObstacleType.Cart: speed = d.cartSpeed; break;
                case ObstacleType.Ball: speed = d.ballSpeed; bounceHeight = d.ballBounceHeight; bouncePeriod = Mathf.Max(0.3f, d.ballBouncePeriod); break;
                case ObstacleType.Plane: speed = d.planeSpeed; baseY += flightAltitude; break;
                default: speed = 0f; break;
            }
            transform.position = new Vector3(x, baseY, layout.playZ);
            if (visual && type != ObstacleType.Radio) visual.localRotation = Quaternion.Euler(0f, Direction > 0 ? 0f : 180f, 0f);
            Active = true;
            gameObject.SetActive(true);
            Physics.SyncTransforms();
        }

        public void Deactivate()
        {
            Active = false;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!Active) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            t += dt;
            var p = transform.position;
            p.x += Direction * speed * dt;
            if (type == ObstacleType.Ball)
            {
                float phase = (t / bouncePeriod) % 1f;
                p.y = baseY + bounceHeight * 4f * phase * (1f - phase); // parábola determinística
                if (visual) visual.Rotate(Vector3.forward, -Direction * speed / Mathf.Max(0.05f, rollRadius) * Mathf.Rad2Deg * dt, Space.World);
            }
            if (propeller) propeller.Rotate(Vector3.forward, 1440f * dt, Space.Self);
            transform.position = p;
        }

        public Bounds HitBounds
        {
            get
            {
                var b = hitCollider ? hitCollider.bounds : new Bounds(transform.position, Vector3.one * 0.5f);
                b.size = Vector3.Scale(b.size, new Vector3(hitShrink, hitShrink, 1f));
                return b;
            }
        }

        public bool Overlaps(Bounds player)
        {
            var b = HitBounds;
            // Teste somente no plano lateral (X/Y): Z é fixo.
            return b.min.x < player.max.x && b.max.x > player.min.x && b.min.y < player.max.y && b.max.y > player.min.y;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            var b = HitBounds;
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
