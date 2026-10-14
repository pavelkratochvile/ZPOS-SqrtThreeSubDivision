using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MeshController : MonoBehaviour
{
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private Button subDivideButton;

    private HalfEdgeMesh halfEdgeMesh;
    
    void Start()
    {
        this.meshFilter = GetComponentInChildren<MeshFilter>();

        if(subDivideButton != null)
        {
            subDivideButton.onClick.AddListener(SubdivideMesh);
        }
        
        // 1. Získání původních dat ze sítě
        Vector3[] originalVertices = meshFilter.mesh.vertices;
        int[] originalIndices = meshFilter.mesh.triangles;
        
        // 2. Sloučení duplicitních vrcholů (svaření)
        WeldMesh(originalVertices, originalIndices, out Vector3[] weldedVertices, out int[] weldedIndices);

        // 3. Inicializace Half-Edge s opravenými (svařenými) daty
        halfEdgeMesh = new HalfEdgeMesh(weldedVertices, weldedIndices);

        Debug.Log(halfEdgeMesh.getVertices().Length);
        
    }

    private void SubdivideMesh()
    {
        this.halfEdgeMesh.subDivide();
        Debug.Log(halfEdgeMesh.getVertices().Length);
    }

    // Pomocná metoda pro sloučení rozpojených vrcholů
    private void WeldMesh(Vector3[] originalVertices, int[] originalIndices, out Vector3[] weldedVertices, out int[] weldedIndices)
    {
        List<Vector3> newVertices = new List<Vector3>();
        Dictionary<Vector3, int> vertexMap = new Dictionary<Vector3, int>();
        weldedIndices = new int[originalIndices.Length];

        for (int i = 0; i < originalIndices.Length; i++)
        {
            Vector3 pos = originalVertices[originalIndices[i]];
            
            // Zaokrouhlení pro eliminaci nepřesností pohyblivé řádové čárky (float)
            Vector3 roundedPos = new Vector3(
                Mathf.Round(pos.x * 1000f) / 1000f,
                Mathf.Round(pos.y * 1000f) / 1000f,
                Mathf.Round(pos.z * 1000f) / 1000f
            );

            // Pokud tento vrchol ještě nemáme, přidáme ho
            if (!vertexMap.TryGetValue(roundedPos, out int newIndex))
            {
                newIndex = newVertices.Count;
                newVertices.Add(pos);
                vertexMap.Add(roundedPos, newIndex);
            }

            // Zapíšeme opravený index pro daný trojúhelník
            weldedIndices[i] = newIndex;
        }

        weldedVertices = newVertices.ToArray();
    }
}