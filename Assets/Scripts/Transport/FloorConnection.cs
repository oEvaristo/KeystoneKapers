using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KeystoneKapers
{
    public interface IConnectionRider
    {
        Transform RiderTransform { get; }
        void OnRideStart(FloorConnection connection);
        void OnRideEnd(FloorConnection connection);
    }

    /// <summary>
    /// Conexão entre andares percorrida por uma trajetória (escada rolante).
    /// Escadas de subida podem ser usadas pelo policial e pelo fugitivo; as de descida são exclusivas do fugitivo.
    /// Os pontos da trajetória são Transforms filhos ajustáveis no Inspector.
    /// </summary>
    public class FloorConnection : MonoBehaviour
    {
        public int fromFloor;
        public int toFloor = 1;
        public bool policeCanUse = true;
        public bool fugitiveCanUse = true;
        [Tooltip("Pontos da trajetória: [0] entrada no corredor, [1] início da rampa, [2] fim da rampa, [3] saída no corredor.")]
        public Transform[] path;
        [Tooltip("Meia-largura (X) da zona de entrada.")]
        public float entryHalfWidth = 0.45f;

        readonly Dictionary<IConnectionRider, Coroutine> riders = new Dictionary<IConnectionRider, Coroutine>();

        public Vector3 EntryPosition => path[0].position;
        public Vector3 ExitPosition => path[path.Length - 1].position;
        public bool IsUp => toFloor > fromFloor;
        /// <summary>Direção horizontal (+1/-1) em que o personagem precisa andar para entrar.</summary>
        public float EntryDirection => Mathf.Sign(path[1].position.x - path[0].position.x + 0.0001f);

        public bool IsRiding(IConnectionRider r) => riders.ContainsKey(r);

        /// <summary>Comprimento total da trajetória (m).</summary>
        public float PathLength
        {
            get
            {
                float d = 0f;
                for (int i = 0; i < path.Length - 1; i++) d += Vector3.Distance(path[i].position, path[i + 1].position);
                return d;
            }
        }

        public bool InEntryZone(Vector3 pos) => Mathf.Abs(pos.x - EntryPosition.x) <= entryHalfWidth && Mathf.Abs(pos.y - EntryPosition.y) < 0.6f;

        public bool TryBegin(IConnectionRider rider, float speed)
        {
            if (rider == null || riders.ContainsKey(rider)) return false;
            riders[rider] = StartCoroutine(Ride(rider, speed));
            return true;
        }

        public void Cancel(IConnectionRider rider)
        {
            if (rider != null && riders.TryGetValue(rider, out var co))
            {
                if (co != null) StopCoroutine(co);
                riders.Remove(rider);
            }
        }

        IEnumerator Ride(IConnectionRider rider, float speed)
        {
            rider.OnRideStart(this);
            var t = rider.RiderTransform;
            Vector3 pos = t.position;
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 target = path[i].position;
                while ((target - pos).sqrMagnitude > 0.0001f)
                {
                    pos = Vector3.MoveTowards(pos, target, speed * Time.deltaTime);
                    t.position = pos;
                    yield return null;
                }
            }
            riders.Remove(rider);
            rider.OnRideEnd(this);
        }

        void OnDrawGizmos()
        {
            if (path == null) return;
            Gizmos.color = policeCanUse ? Color.cyan : new Color(1f, 0.4f, 0.1f);
            for (int i = 0; i < path.Length - 1; i++)
                if (path[i] && path[i + 1]) Gizmos.DrawLine(path[i].position, path[i + 1].position);
            if (path.Length > 0 && path[0]) Gizmos.DrawWireCube(path[0].position + Vector3.up * 0.8f, new Vector3(entryHalfWidth * 2, 1.6f, 0.5f));
        }
    }
}
