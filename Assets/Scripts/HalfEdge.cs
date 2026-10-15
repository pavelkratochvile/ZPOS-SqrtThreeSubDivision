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

        // Přidat na úplný konec konstruktoru HalfEdgeMesh:
        for (int i = 0; i < this.edges.Count; i++)
        {
            // Pokud hrana leží na okraji díry (nemá souseda)
            if (this.edges[i].twinEdgeIndex == -1)
            {
                int vIdx = this.edges[i].fromVertexIndex;
                HalfEdgeVertex v = this.vertices[vIdx];

                // Vynutíme, aby vrchol začínal obíhání z hrany na okraji!
                v.halfEdgeIndex = i;
                this.vertices[vIdx] = v;
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
        bool isClosed = false;

        do
        {
            // Vrchol, kam ukazuje currEdge (náš soused)
            int neighborIndex = this.edges[this.edges[currEdge].nextEdgeIndex].fromVertexIndex;
            neighboringVertices.Add(neighborIndex);

            // Hledáme hranu vstupující do našeho vrcholu (prev = next -> next)
            int next = this.edges[currEdge].nextEdgeIndex;
            int prev = this.edges[next].nextEdgeIndex;

            // Přechod do sousedního trojúhelníku
            currEdge = this.edges[prev].twinEdgeIndex;

            if (currEdge == startEdge)
            {
                isClosed = true;
                break;
            }

            if (currEdge == -1)
            {
                int boundaryNeighbor = this.edges[prev].fromVertexIndex;
                neighboringVertices.Add(boundaryNeighbor);
                break;
            }

        } while (true);

        if (isClosed)
        {
            return neighboringVertices;
        }

        List<int> reverseNeighbors = new List<int>();
        currEdge = startEdge;

        while (this.edges[currEdge].twinEdgeIndex != -1)
        {
            int twin = this.edges[currEdge].twinEdgeIndex;
            currEdge = this.edges[twin].nextEdgeIndex;

            if (currEdge == startEdge)
                break;

            int neighborIndex = this.edges[this.edges[currEdge].nextEdgeIndex].fromVertexIndex;
            reverseNeighbors.Add(neighborIndex);
        }
        reverseNeighbors.Reverse();
        reverseNeighbors.AddRange(neighboringVertices);

        return reverseNeighbors;
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

        for (int e = 0; e < this.edges.Count; e++)
        {
            if (this.edges[e].twinEdgeIndex == -1)
            {
                int vIdx = this.edges[e].fromVertexIndex;
                HalfEdgeVertex v = this.vertices[vIdx];
                v.halfEdgeIndex = e;
                this.vertices[vIdx] = v;
            }
        }
    }

    private void smooth()
    {
        // Dočasné pole, abychom nepřepisovali pozice pod rukama!
        Vector3[] newPositions = new Vector3[this.vertices.Count];

        for (int i = 0; i < this.vertices.Count; i++)
        {
            List<int> neibs = this.VVQuery(i);
            if (neibs.Count == 0)
            {
                newPositions[i] = this.vertices[i].position;
                continue;
            }

            int startEdge = this.vertices[i].halfEdgeIndex;
            bool isBoundary = (startEdge != -1 && this.edges[startEdge].twinEdgeIndex == -1);

            if (isBoundary && neibs.Count >= 2)
            {
                int leftNeighbor = neibs[0];
                int rightNeighbor = neibs[neibs.Count - 1];

                Vector3 boundaryPos = (1f / 6f) * this.vertices[leftNeighbor].position
                                    + (4f / 6f) * this.vertices[i].position
                                    + (1f / 6f) * this.vertices[rightNeighbor].position;

                newPositions[i] = boundaryPos;
                continue;
            }

            Vector3 sum = Vector3.zero;

            for (int j = 0; j < neibs.Count; j++)
            {
                sum += this.vertices[neibs[j]].position;
            }

            int n = neibs.Count;
            
            // Důležité: 'f' za čísly, aby se neaplikovalo celočíselné dělení
            float alpha = (4f - 2f * Mathf.Cos((2f * Mathf.PI) / n)) / 9f;
            Vector3 newP = (1f - alpha) * this.vertices[i].position + (alpha / n) * sum;

            newPositions[i] = newP;
        }

        // AŽ TEĎ, když mají všechny vrcholy spočítáno, je hromadně přesuneme
        for (int i = 0; i < this.vertices.Count; i++)
        {
            if (this.vertices[i].isOriginal)
            {
                HalfEdgeVertex v = this.vertices[i];
                v.position = newPositions[i];
                this.vertices[i] = v;
            }
        }
    }

    private void flip()
    {
        for(int i = 0; i < this.edges.Count; i++)
        {
            HalfEdgeEdge e = this.edges[i];

            if (!e.isOriginal || i >= e.twinEdgeIndex || e.twinEdgeIndex == -1)
            {
                continue;
            }

            int f00i = i;
            int f01i = this.edges[i].nextEdgeIndex;
            int f02i = this.edges[f01i].nextEdgeIndex;
            int f10i = this.edges[f00i].twinEdgeIndex;
            int f11i = this.edges[f10i].nextEdgeIndex;
            int f12i = this.edges[f11i].nextEdgeIndex;

            int oldV0 = e.fromVertexIndex;
            int oldV1 = this.edges[f10i].fromVertexIndex;

            // new f00/f10 from vertex index
            int nf00fvi = this.edges[f12i].fromVertexIndex;
            int nf10fvi = this.edges[f02i].fromVertexIndex;
            
            // new faces
            int nf00fi = this.edges[f00i].faceIndex;
            int nf10fi = this.edges[f10i].faceIndex;

            HalfEdgeEdge nf00 = new HalfEdgeEdge(nf00fvi,nf00fi,f02i,f10i,false);
            HalfEdgeEdge nf10 = new HalfEdgeEdge(nf10fvi,nf10fi,f12i,f00i,false);
            this.edges[f00i] = nf00;
            this.edges[f10i] = nf10;

            // edges adjustment
            HalfEdgeEdge f02 = this.edges[f02i];
            f02.nextEdgeIndex = f11i;
            this.edges[f02i] = f02;

            HalfEdgeEdge f11 = this.edges[f11i];
            f11.nextEdgeIndex = f00i;
            f11.faceIndex = nf00fi;
            this.edges[f11i] = f11;

            HalfEdgeEdge f12 = this.edges[f12i];
            f12.nextEdgeIndex = f01i;
            this.edges[f12i] = f12;

            HalfEdgeEdge f01 = this.edges[f01i];
            f01.nextEdgeIndex = f10i;
            f01.faceIndex = nf10fi;
            this.edges[f01i] = f01;

            // Aktualizace referenčních hran pro stěny
            this.faces[nf00fi] = new HalfEdgeFace(f00i);
            this.faces[nf10fi] = new HalfEdgeFace(f10i);

            // Pojistka pro vrcholy: aby neukazovaly na hranu, která už z nich nevychází
            HalfEdgeVertex v0 = this.vertices[oldV0];
            v0.halfEdgeIndex = f01i;
            this.vertices[oldV0] = v0;

            HalfEdgeVertex v1 = this.vertices[oldV1];
            v1.halfEdgeIndex = f11i;
            this.vertices[oldV1] = v1;
        }
    }

    private void resetOriginalFlags()
    {
        for(int i = 0; i < this.edges.Count; i++)
        {
            HalfEdgeEdge e = this.edges[i];
            e.isOriginal = true;
            this.edges[i] = e;
        }

        for(int i = 0; i < this.vertices.Count; i++)
        {
            HalfEdgeVertex v = this.vertices[i];
            v.isOriginal = true;
            this.vertices[i] = v;
        }
    }

    public void subDivide()
    {
        this.smooth();
        this.split();
        this.flip();
        this.resetOriginalFlags();
    }

}