using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Presentation
{
    /// <summary>
    /// Polls the run's obstacle state on the existing arena mesh. Cracks and rubble have no
    /// colliders; the analytic obstacle list remains the only authority for combat.
    /// </summary>
    public sealed class DecayingPillarView : MonoBehaviour
    {
        ArenaSim sim;
        int index;
        Renderer solid;
        GameObject firstCracks, lastCracks, rubble;
        Material crackMaterial;

        public void Bind(ArenaSim run, int pillarIndex)
        {
            sim = run;
            index = pillarIndex;
            if (solid != null) return;
            solid = GetComponent<Renderer>();
            crackMaterial = new Material(run.Config.unlitMaterial != null ? run.Config.unlitMaterial
                : solid.sharedMaterial);
            crackMaterial.color = new Color(0.11f, 0.07f, 0.12f);
            firstCracks = new GameObject("FirstCracks");
            firstCracks.transform.SetParent(transform, false);
            Piece(firstCracks.transform, new Vector3(-0.12f, 0.16f, -0.507f), new Vector3(0.025f, 0.42f, 0.012f), 24, crackMaterial);
            Piece(firstCracks.transform, new Vector3(-0.04f, -0.13f, -0.507f), new Vector3(0.025f, 0.24f, 0.012f), -32, crackMaterial);
            lastCracks = new GameObject("LastCracks");
            lastCracks.transform.SetParent(transform, false);
            Piece(lastCracks.transform, new Vector3(0.18f, -0.05f, -0.509f), new Vector3(0.03f, 0.67f, 0.012f), -21, crackMaterial);
            Piece(lastCracks.transform, new Vector3(0.27f, 0.12f, -0.509f), new Vector3(0.25f, 0.025f, 0.012f), 22, crackMaterial);
            rubble = new GameObject("Rubble");
            rubble.transform.SetParent(transform, false);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2 / 5;
                Piece(rubble.transform, new Vector3(Mathf.Cos(angle) * 0.3f, -0.44f, Mathf.Sin(angle) * 0.3f),
                    new Vector3(0.27f, 0.12f, 0.23f), i * 33, solid.sharedMaterial);
            }
        }

        static void Piece(Transform parent, Vector3 at, Vector3 scale, float angle, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(0, 0, angle);
            go.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(go.GetComponent<Collider>());
        }

        void LateUpdate()
        {
            if (sim == null || index >= sim.Pillars.Count) return;
            var state = sim.Pillars[index];
            solid.enabled = !state.Crumbled;
            firstCracks.SetActive(!state.Crumbled && state.Durability <= 8);
            lastCracks.SetActive(!state.Crumbled && state.Durability <= 4);
            rubble.SetActive(state.Crumbled);
        }

        void OnDestroy() { if (crackMaterial != null) Destroy(crackMaterial); }
    }
}
