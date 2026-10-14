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
        UnWeldMesh();
    }

    private void UnWeldMesh()
    {
        Vector3[] weldedVertices = halfEdgeMesh.getVertices();
        int[] weldedIndices = halfEdgeMesh.getIndices();

        // Počet nových vrcholů odpovídá přesně počtu indexů (každý trojúhelník má 3 unikátní vrcholy)
        Vector3[] flatVertices = new Vector3[weldedIndices.Length];
        int[] flatIndices = new int[weldedIndices.Length];

        for (int i = 0; i < weldedIndices.Length; i++)
        {
            flatVertices[i] = weldedVertices[weldedIndices[i]];
            flatIndices[i] = i; // Indexy jdou lineárně za sebou: 0, 1, 2, 3, 4, 5...
        }

        Mesh mesh = new Mesh();

        // Pojistka: pokud má model po subdivizi více než 65 535 vrcholů,
        // musíme Unity říct, aby použilo 32bitové indexy, jinak by mesh spadl.
        if (flatVertices.Length > 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.vertices = flatVertices;
        mesh.triangles = flatIndices;

        // Přepočítá kolmé normály zvlášť pro každou plošku (vytvoří ostré hrany)
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        this.meshFilter.mesh = mesh;
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