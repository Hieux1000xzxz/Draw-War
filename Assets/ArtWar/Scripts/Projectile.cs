using System;
using UnityEngine;

namespace ArtWar
{
    /// <summary>A paper shot (arrow, shell, bomb, fire puff) that flies from A to B along an arc, then calls onHit.</summary>
    public class Projectile : MonoBehaviour
    {
        Vector3 from, to;
        float duration, age, arcHeight;
        bool alignToVelocity;
        Action onHit;

        public static void Launch(Doodle look, float scale, Vector3 from, Vector3 to, float speed, float arcHeight,
            bool alignToVelocity, Action onHit, Color? crayon = null)
        {
            // online: the guest sees the same shot fly (just for show; the host decides what it hits)
            if (NetSession.IsHost)
            {
                NetSession.Send(NetSession.Msg.Shot, w =>
                {
                    NetSession.WriteDoodle(w, look);
                    w.WriteValueSafe(scale);
                    w.WriteValueSafe(from);
                    w.WriteValueSafe(to);
                    w.WriteValueSafe(speed);
                    w.WriteValueSafe(arcHeight);
                    w.WriteValueSafe(alignToVelocity);
                    w.WriteValueSafe(crayon.HasValue);
                    w.WriteValueSafe(crayon ?? Color.white);
                });
            }

            var go = new GameObject("Projectile");
            go.transform.position = from;
            var built = crayon.HasValue
                ? DoodleBuilder.BuildScenery(look, crayon.Value, scale, null, 0.95f)
                : DoodleBuilder.Build(look, false, null, scale, false);
            var visual = built.root.transform;
            visual.SetParent(go.transform, false);
            visual.localPosition = new Vector3(0f, -built.size.y * 0.5f, 0f);

            var p = go.AddComponent<Projectile>();
            p.from = from;
            p.to = to;
            p.duration = Mathf.Max(0.08f, Vector3.Distance(from, to) / speed);
            p.arcHeight = arcHeight;
            p.alignToVelocity = alignToVelocity;
            p.onHit = onHit;
        }

        void Update()
        {
            Vector3 prev = transform.position;
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / duration);
            transform.position = Vector3.Lerp(from, to, t) + Vector3.up * (arcHeight * 4f * t * (1f - t));

            if (alignToVelocity)
            {
                Vector3 v = transform.position - prev;
                if (v.sqrMagnitude > 1e-6f)
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
            }

            if (t >= 1f)
            {
                onHit?.Invoke();
                Destroy(gameObject);
            }
        }

        void OnDestroy() => DoodleBuilder.DestroyMeshes(gameObject);
    }
}
