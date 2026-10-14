using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using Unity.VisualScripting;

public class FaceVertexIndices
{
    int id0;
    int id1;
    int id2;

    public FaceVertexIndices(int id0, int id1, int id2)
    {
        this.id0 = id0;
        this.id1 = id1;
        this.id2 = id2;
    }
}
public struct HalfEdgeEdge
{
    public int fromVertexIndex;
    public int faceIndex;
    public int nextEdgeIndex;
    public int twinEdgeIndex;
    public bool isOriginal;

    public HalfEdgeEdge(int fromVertexIndex, int faceIndex, int nextEdgeIndex, int twinEdgeIndex, bool isOriginal)
    {
        this.fromVertexIndex = fromVertexIndex;
        this.faceIndex = faceIndex;
        this.nextEdgeIndex = nextEdgeIndex;
        this.twinEdgeIndex = twinEdgeIndex;
        this.isOriginal = isOriginal;
    }
}

public struct HalfEdgeFace
{
    public int edgeIndex;

    public HalfEdgeFace(int edgeIndex)
    {
        this.edgeIndex = edgeIndex;
    }
}

public struct HalfEdgeVertex
{
    public Vector3 position;
    public int halfEdgeIndex;
    public bool isOriginal;

    public HalfEdgeVertex(Vector3 position, int halfEdgeIndex, bool isOriginal)
    {
        this.position = position;
        this.halfEdgeIndex = halfEdgeIndex;
        this.isOriginal = isOriginal;
    }
}

public class HalfEdgeMesh
{
    public List<HalfEdgeEdge> edges;
    public List<HalfEdgeFace> faces;
    public List<HalfEdgeVertex> vertices;

    public HalfEdgeMesh(Vector3[] vertices, int[] indices)
    {
        this.vertices = new List<HalfEdgeVertex>(vertices.Length);
        this.edges = new List<HalfEdgeEdge>(indices.Length * 2);
        this.faces = new List<HalfEdgeFace>(indices.Length / 3);

        // Inicializace seznamu vertexů
        for(int i = 0; i < vertices.Length; i++)
        {
            this.vertices.Add(new HalfEdgeVertex(vertices[i], -1, true));
        }

        // Vytvoření polohran a faces
        Dictionary<(int fromVertex, int toVertex), int> edgeLookup = new Dictionary<(int, int), int>();
        int trianglesCount = indices.Length / 3;
        for(int t = 0; t < trianglesCount; t++)
        {
            int id0 = indices[3 * t + 0];
            int id1 = indices[3 * t + 1];
            int id2 = indices[3 * t + 2];

            int baseTriangleIndex = t;
            int baseEdgeIndex = 3 * t;

            this.faces.Add(new HalfEdgeFace(baseTriangleIndex));
            this.edges.Add(new HalfEdgeEdge(id0, baseTriangleIndex, baseEdgeIndex + 1, -1, true));
            this.edges.Add(new HalfEdgeEdge(id1, baseTriangleIndex, baseEdgeIndex + 2, -1, true)); 
            this.edges.Add(new HalfEdgeEdge(id2, baseTriangleIndex, baseEdgeIndex + 0, -1, true));

            setOutGoingEdge(id0, baseEdgeIndex + 0);
            setOutGoingEdge(id1, baseEdgeIndex + 1);
            setOutGoingEdge(id2, baseEdgeIndex + 2);

            edgeLookup[(id0, id1)] = baseEdgeIndex + 0;
            edgeLookup[(id1, id2)] = baseEdgeIndex + 1;
            edgeLookup[(id2, id0)] = baseEdgeIndex + 2;
        }

        // Přidání opačných polohran
        for(int i = 0; i < this.edges.Count; i++)
        {
            HalfEdgeEdge heFrom = this.edges[i];
            HalfEdgeEdge heNext = this.edges[heFrom.nextEdgeIndex];
            
            if(heFrom.twinEdgeIndex != -1)
            {
                continue;
            }

            int fromVerIndex = heFrom.fromVertexIndex;
            int toVerIndex = heNext.fromVertexIndex;

            if(edgeLookup.TryGetValue((toVerIndex, fromVerIndex), out int twinEdgeIndex))
            {
                heFrom.twinEdgeIndex = twinEdgeIndex;
                this.edges[i] = heFrom;

                HalfEdgeEdge heTwin = this.edges[twinEdgeIndex];
                heTwin.twinEdgeIndex = i;   
                this.edges[twinEdgeIndex] = heTwin;
            }
        }
    }

    private void setOutGoingEdge(int vertexIndex, int edgeIndex)
    {
        HalfEdgeVertex v = this.vertices[vertexIndex];
        if(v.halfEdgeIndex == -1)
        {
            v.halfEdgeIndex = edgeIndex;
            this.vertices[vertexIndex] = v;
        }
    }

    public void writeInfo()
    {
        HalfEdgeVertex hev = vertices[0];
        Vector3 position = hev.position;
        int halfEdgeIndex = hev.halfEdgeIndex;
        bool isOriginal = hev.isOriginal;
        Debug.Log(position);
        Debug.Log(halfEdgeIndex);
        Debug.Log(isOriginal);
    }

    public FaceVertexIndices FVQuery(int faceIndex)
    {
        HalfEdgeFace hef = this.faces[faceIndex];
        HalfEdgeEdge he0 = this.edges[hef.edgeIndex];
        HalfEdgeEdge he1 = this.edges[he0.nextEdgeIndex];
        HalfEdgeEdge he2 = this.edges[he1.nextEdgeIndex];

        return new FaceVertexIndices(he0.fromVertexIndex, he1.fromVertexIndex, he2.fromVertexIndex);
    }
    public List<int> VVQuery(int vertexIndex)
    {
        List<int> neighboringVertices = new List<int>();

        int startEdge = this.vertices[vertexIndex].halfEdgeIndex;
        if (startEdge == -1)
            return neighboringVertices;

        int currEdge = startEdge;

        do
        {
            // 1. Soused je vrchol, kam směřuje currEdge
            int neighborIndex = this.edges[this.edges[currEdge].nextEdgeIndex].fromVertexIndex;
            neighboringVertices.Add(neighborIndex);

            // 2. Najdeme hranu v tomtéž trojúhelníku, která do našeho vrcholu vstupuje (prevEdge = next -> next)
            int next = this.edges[currEdge].nextEdgeIndex;
            int prev = this.edges[next].nextEdgeIndex;

            // 3. Přeskočíme do sousedního trojúhelníku
            currEdge = this.edges[prev].twinEdgeIndex;

            // Ošetření hranice sítě (otevřený mesh)
            if (currEdge == -1)
                break;

        } while (currEdge != startEdge);

        return neighboringVertices;
    }

    public void printVerNeiborhood(int vertexIndex)
    {
        List<int> neighbors = VVQuery(vertexIndex);
        string neighborStr = string.Join(", ", neighbors);
        Debug.Log($"Neighbors of vertex {vertexIndex}: {neighborStr}");
    }
}