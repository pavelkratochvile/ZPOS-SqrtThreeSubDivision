using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using Unity.VisualScripting;
using System;


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

            this.faces.Add(new HalfEdgeFace(baseEdgeIndex));
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

    public Vector3[] getVertices()
    {
        Vector3[] v = new Vector3[this.vertices.Count];

        for(int i = 0; i < this.vertices.Count; i++)
        {
            v[i] = this.vertices[i].position;
        }

        return v;
    }

    public int[] getIndices()
    {
        int[] ind = new int[this.faces.Count * 3];

        for(int i = 0; i < this.faces.Count; i++)
        {
            HalfEdgeEdge h0 = this.edges[this.faces[i].edgeIndex];
            HalfEdgeEdge h1 = this.edges[h0.nextEdgeIndex];
            HalfEdgeEdge h2 = this.edges[h1.nextEdgeIndex];

            ind[i * 3 + 0] = h0.fromVertexIndex;
            ind[i * 3 + 1] = h1.fromVertexIndex;
            ind[i * 3 + 2] = h2.fromVertexIndex;
        }
        
        return ind;
    }

    private void split()
    {
        int originalFaceCount = this.faces.Count;

        for(int i = 0; i < originalFaceCount; i++)
        {
            HalfEdgeEdge h0 = this.edges[this.faces[i].edgeIndex];
            HalfEdgeEdge h1 = this.edges[h0.nextEdgeIndex];
            HalfEdgeEdge h2 = this.edges[h1.nextEdgeIndex];

            Vector3 vSum = this.vertices[h0.fromVertexIndex].position + this.vertices[h1.fromVertexIndex].position + this.vertices[h2.fromVertexIndex].position;
            Vector3 vNew = vSum / 3;

            int h00i = this.faces[i].edgeIndex;
            int h10i = h0.nextEdgeIndex;
            int h20i = h1.nextEdgeIndex;
            int h01i = this.edges.Count + 0;
            int h12i = this.edges.Count + 1;
            int h11i = this.edges.Count + 2;
            int h22i = this.edges.Count + 3;
            int h21i = this.edges.Count + 4;
            int h02i = this.edges.Count + 5;

            int t0 = i;
            int t1 = this.faces.Count + 0;
            int t2 = this.faces.Count + 1;

            int p0 = h0.fromVertexIndex;
            int p1 = h1.fromVertexIndex;
            int p2 = h2.fromVertexIndex;

            this.vertices.Add(new HalfEdgeVertex(vNew, h02i, false));
            int newVertexIndex = this.vertices.Count - 1;

            //pridani h01
            this.edges.Add(new HalfEdgeEdge(p1,t0,h02i,h12i,false));
            //pridani h12
            this.edges.Add(new HalfEdgeEdge(newVertexIndex,t1,h10i,h01i,false));
            //pridani h11
            this.edges.Add(new HalfEdgeEdge(p2,t1,h12i,h22i,false));
            //pridani h22
            this.edges.Add(new HalfEdgeEdge(newVertexIndex,t2,h20i,h11i,false));
            //pridani h21
            this.edges.Add(new HalfEdgeEdge(p0,t2,h22i,h02i,false));
            //pridani h02
            this.edges.Add(new HalfEdgeEdge(newVertexIndex,t0,h00i,h21i,false));

            h0.nextEdgeIndex = h01i;
            this.edges[h00i] = h0;

            h1.nextEdgeIndex = h11i;
            h1.faceIndex = t1;
            this.edges[h10i] = h1;

            h2.nextEdgeIndex = h21i;
            h2.faceIndex = t2;
            this.edges[h20i] = h2;

            this.faces.Add(new HalfEdgeFace(h10i));
            this.faces.Add(new HalfEdgeFace(h20i));
        }
    }

    private void smooth()
    {
        List<int> neibs = new List<int>();
        
        for(int i = 0; i < this.vertices.Count; i++)
        {
            if(this.vertices[i].isOriginal == false)
            {
                continue;
            }
            
            neibs = this.VVQuery(i);
            Vector3 sum = new Vector3();

            for(int j = 0; j < neibs.Count; j++)
            {
                sum += this.vertices[neibs[j]].position;
            }

            int n = neibs.Count;
            float alpha = (4 - 2 * Mathf.Cos((2 * Mathf.PI)/n)) / 9;
            Vector3 newP = (1 - alpha) * this.vertices[i].position + alpha/n * sum;
            
            HalfEdgeVertex v = this.vertices[i];
            v.position = newP;
            this.vertices[i] = v;
        }
    }

    public void subDivide()
    {
        this.split();
        this.smooth();
    }

}