using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;

public class MeshController : MonoBehaviour
{
    // Komponent sítě, jejíž geometrii budeme upravovat
    [SerializeField] private MeshFilter meshFilter;
    // Tlačítko pro spuštění další iterace subdivize
    [SerializeField] private Button subDivideButton;
    // Rychlost otáčení modelu při ovládání klávesnicí
    [SerializeField] private float rotationSpeed = 90f;
    // Volba, zda se mají dělit také hraniční trojúhelníky
    [SerializeField] private bool divideBorderTriangles;
    // Váha určující zachování ostrých hran na hranici sítě
    [SerializeField] private float borderCreaseWeight;
    // Volba nelineárního vyhlazení hraničních vrcholů
    [SerializeField] private bool nonLinearBoundarySmoothing;
    // Volba výpočtu ostrosti hran podle dihedrálního úhlu
    [SerializeField] private bool dihedralAngleBasedSmooting;
    // Dolní mez dihedrálního úhlu pro výpočet ostrosti
    [SerializeField] private float minDihedralAngle;
    // Horní mez dihedrálního úhlu pro výpočet ostrosti
    [SerializeField] private float maxDihedralAngle;

    // Half-Edge reprezentace upravované sítě
    private HalfEdgeMesh halfEdgeMesh;

    // Metoda pro otáčení modelu pomocí šipek na klávesnici
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        float horizontalInput = 0f;
        if (keyboard.leftArrowKey.isPressed)
        {
            horizontalInput -= 1f;
        }
        if (keyboard.rightArrowKey.isPressed)
        {
            horizontalInput += 1f;
        }

        float verticalInput = 0f;
        if (keyboard.downArrowKey.isPressed)
        {
            verticalInput -= 1f;
        }
        if (keyboard.upArrowKey.isPressed)
        {
            verticalInput += 1f;
        }

        Vector3 rotation = new Vector3(
            -verticalInput,
            horizontalInput,
            0f
        );

        transform.Rotate(rotation * rotationSpeed * Time.deltaTime, Space.Self);
    }
    
    // Metoda pro inicializaci sítě a obsluhu tlačítka subdivize
    void Start()
    {
        this.meshFilter = GetComponentInChildren<MeshFilter>();

        if(subDivideButton != null)
        {
            subDivideButton.onClick.AddListener(SubdivideMesh);
        }
        
        // Načtení pozic vrcholů a indexů trojúhelníků původního meshe
        Vector3[] originalVertices = meshFilter.mesh.vertices;
        int[] originalIndices = meshFilter.mesh.triangles;
        
        // Sloučení vrcholů se stejnou polohou, které jsou v meshi oddělené
        WeldMesh(originalVertices, originalIndices, out Vector3[] weldedVertices, out int[] weldedIndices);

        // Vytvoření Half-Edge reprezentace ze svařených dat
        halfEdgeMesh = new HalfEdgeMesh(weldedVertices, weldedIndices, dihedralAngleBasedSmooting, minDihedralAngle, maxDihedralAngle);
        SetWireframe(meshFilter.mesh);

        Debug.Log(halfEdgeMesh.getVertices().Length);
        
    }

    // Metoda pro provedení subdivize a aktualizaci zobrazeného meshe
    private void SubdivideMesh()
    {
        this.halfEdgeMesh.subDivide(this.divideBorderTriangles, this.borderCreaseWeight, this.nonLinearBoundarySmoothing);
        Debug.Log(halfEdgeMesh.getVertices().Length);
        UnWeldMeshFlat();
    }

    // Metoda pro vytvoření meshe se sdílenými vrcholy a vyhlazenými normálami
    private void UnWeldMeshSmooth()
    {
        Mesh mesh = new Mesh();

        // Pro více než 65 535 vrcholů je nutné použít 32bitové indexy
        if (halfEdgeMesh.vertices.Count > 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.vertices = halfEdgeMesh.getVertices();
        mesh.triangles = halfEdgeMesh.getIndices();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        SetWireframe(mesh);

        this.meshFilter.mesh = mesh;
    }

    // Metoda pro vytvoření meshe s oddělenými vrcholy jednotlivých stěn
    private void UnWeldMeshFlat()
    {
        Vector3[] weldedVertices = halfEdgeMesh.getVertices();
        int[] weldedIndices = halfEdgeMesh.getIndices();

        // Každý index dostane vlastní vrchol, aby se normály mezi stěnami nesdílely
        Vector3[] flatVertices = new Vector3[weldedIndices.Length];
        int[] flatIndices = new int[weldedIndices.Length];

        for (int i = 0; i < weldedIndices.Length; i++)
        {
            flatVertices[i] = weldedVertices[weldedIndices[i]];
            // Indexy nových vrcholů tvoří souvislou posloupnost
            flatIndices[i] = i;
        }

        Mesh mesh = new Mesh();

        // Pro více než 65 535 vrcholů je nutné použít 32bitové indexy
        if (flatVertices.Length > 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.vertices = flatVertices;
        mesh.triangles = flatIndices;

        // Přepočet normál pro oddělené vrcholy zachová ostré hrany mezi stěnami
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        SetWireframe(mesh);

        this.meshFilter.mesh = mesh;
    }

    // Metoda pro vytvoření drátového zobrazení hran trojúhelníků
    private void SetWireframe(Mesh mesh)
    {
        int[] triangles = mesh.triangles;
        int[] lines = new int[triangles.Length * 2];

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int lineIndex = i * 2;

            lines[lineIndex] = triangles[i];
            lines[lineIndex + 1] = triangles[i + 1];

            lines[lineIndex + 2] = triangles[i + 1];
            lines[lineIndex + 3] = triangles[i + 2];

            lines[lineIndex + 4] = triangles[i + 2];
            lines[lineIndex + 5] = triangles[i];
        }

        mesh.SetIndices(lines, MeshTopology.Lines, 0);
    }

    // Metoda pro sloučení vrcholů se stejnou polohou a úpravu jejich indexů
    private void WeldMesh(Vector3[] originalVertices, int[] originalIndices, out Vector3[] weldedVertices, out int[] weldedIndices)
    {
        List<Vector3> newVertices = new List<Vector3>();
        Dictionary<Vector3, int> vertexMap = new Dictionary<Vector3, int>();
        weldedIndices = new int[originalIndices.Length];

        for (int i = 0; i < originalIndices.Length; i++)
        {
            Vector3 pos = originalVertices[originalIndices[i]];
            
            // Zaokrouhlení omezuje rozdíly způsobené nepřesností čísel typu float
            Vector3 roundedPos = new Vector3(
                Mathf.Round(pos.x * 1000f) / 1000f,
                Mathf.Round(pos.y * 1000f) / 1000f,
                Mathf.Round(pos.z * 1000f) / 1000f
            );

            // Dosud neznámou polohu přidáme do seznamu a uložíme její nový index
            if (!vertexMap.TryGetValue(roundedPos, out int newIndex))
            {
                newIndex = newVertices.Count;
                newVertices.Add(pos);
                vertexMap.Add(roundedPos, newIndex);
            }

            // Původní index nahradíme indexem svařeného vrcholu
            weldedIndices[i] = newIndex;
        }

        weldedVertices = newVertices.ToArray();
    }
}