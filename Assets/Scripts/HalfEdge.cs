using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using Unity.VisualScripting;
using System;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using UnityEngine.TextCore;

// Struktuta pro reprezentaci vrcholů stěny
public class FaceVertexIndices
{
    // Indexy vrcholů
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

// Struktuta pro reprezentaci polohrany
public struct HalfEdgeEdge
{
    // Index vrcholu, ze kterého polohrana vychází
    public int fromVertexIndex;
    // Index stěny, které polohrana náleží
    public int faceIndex;
    // Index následující polohrany
    public int nextEdgeIndex;
    // Index sousední polohrany
    public int twinEdgeIndex;
    // Flag pro originalitu polohrany v rámci jedné iterace
    public bool isOriginal;
    // Ostrost polohrany
    public float creaseWeight;

    public HalfEdgeEdge(int fromVertexIndex, int faceIndex, int nextEdgeIndex, int twinEdgeIndex, bool isOriginal, float creaseWeight)
    {
        this.fromVertexIndex = fromVertexIndex;
        this.faceIndex = faceIndex;
        this.nextEdgeIndex = nextEdgeIndex;
        this.twinEdgeIndex = twinEdgeIndex;
        this.isOriginal = isOriginal;
        this.creaseWeight = creaseWeight;
    }
}

// Struktuta pro reprezentaci stěny
public struct HalfEdgeFace
{
    // Index jedné polohrany v trojúhelníku
    public int edgeIndex;

    public HalfEdgeFace(int edgeIndex)
    {
        this.edgeIndex = edgeIndex;
    }
}

// Struktuta pro reprezentaci vrcholu
public struct HalfEdgeVertex
{
    // Poloha vrcholu v prostoru
    public Vector3 position;
    // Původní pozice vrcholu v prostoru v rímci jedné iterace
    public Vector3 oldPosition;
    // Index jedné z vycházejících hran
    public int halfEdgeIndex;
    // Flag pro originalitu vrcholu v rámci jedné iterace
    public bool isOriginal;

    public HalfEdgeVertex(Vector3 position, int halfEdgeIndex, bool isOriginal)
    {
        this.position = position;
        this.oldPosition = position;
        this.halfEdgeIndex = halfEdgeIndex;
        this.isOriginal = isOriginal;
    }
}

// Struktuta pro reprezentaci polygonální sítě (mesh)
public class HalfEdgeMesh
{
    // Seznam polohran sítě
    public List<HalfEdgeEdge> edges;
    // Seznam stěn sítě
    public List<HalfEdgeFace> faces;
    // Seznam vrcholů sítě
    public List<HalfEdgeVertex> vertices;

    public HalfEdgeMesh(Vector3[] vertices, int[] indices, bool dihedralAngleBasedSmooting, float minDihedralAngle, float maxDihedralAngle)
    {
        this.vertices = new List<HalfEdgeVertex>(vertices.Length);
        this.edges = new List<HalfEdgeEdge>(indices.Length * 2);
        this.faces = new List<HalfEdgeFace>(indices.Length / 3);

        // Inicializace seznamu vrcholů
        for(int i = 0; i < vertices.Length; i++)
        {
            this.vertices.Add(new HalfEdgeVertex(vertices[i], -1, true));
        }

        // Vytvoření polohran a stěn
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
            this.edges.Add(new HalfEdgeEdge(id0, baseTriangleIndex, baseEdgeIndex + 1, -1, true, 0f));
            this.edges.Add(new HalfEdgeEdge(id1, baseTriangleIndex, baseEdgeIndex + 2, -1, true, 0f)); 
            this.edges.Add(new HalfEdgeEdge(id2, baseTriangleIndex, baseEdgeIndex + 0, -1, true, 0f));

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
                // Propojíme sousedních polohran pomocí slovníku
                heFrom.twinEdgeIndex = twinEdgeIndex;
                
                HalfEdgeEdge heTwin = this.edges[twinEdgeIndex];
                heTwin.twinEdgeIndex = i;   

                // Výpočet dihedrálního úhlu
                int faceA = heFrom.faceIndex;
                int faceB = heTwin.faceIndex;

                Vector3 normalA = getFaceNormal(faceA);
                Vector3 normalB = getFaceNormal(faceB);

                float angle = Vector3.Angle(normalA, normalB);
                float calculatedCrease = Mathf.InverseLerp(minDihedralAngle, maxDihedralAngle, angle);
                
                // Přiřazení váhy na základě volby uživatele
                if (dihedralAngleBasedSmooting)
                {
                    heFrom.creaseWeight = calculatedCrease;
                    heTwin.creaseWeight = calculatedCrease;
                }
                else
                {
                    heFrom.creaseWeight = 0f;
                    heTwin.creaseWeight = 0f;
                }
                this.edges[i] = heFrom;
                this.edges[twinEdgeIndex] = heTwin;
            }
        }

        // Pro polohrany bez souseda se vynutí obíhání z hrany na okraji
        for (int i = 0; i < this.edges.Count; i++)
        {
            if (this.edges[i].twinEdgeIndex == -1)
            {
                int vIdx = this.edges[i].fromVertexIndex;
                changeVertexEdge(vIdx, i);
            }
        }
    }

    // Pomocná metoda pro získání normály stěny
    private Vector3 getFaceNormal(int faceIndex)
    {
        HalfEdgeEdge e0 = this.edges[this.faces[faceIndex].edgeIndex];
        HalfEdgeEdge e1 = this.edges[e0.nextEdgeIndex];
        HalfEdgeEdge e2 = this.edges[e1.nextEdgeIndex];
        
        Vector3 p0 = this.vertices[e0.fromVertexIndex].position;
        Vector3 p1 = this.vertices[e1.fromVertexIndex].position;
        Vector3 p2 = this.vertices[e2.fromVertexIndex].position;

        Vector3 U = p1 - p0;
        Vector3 V = p2 - p0;

        return Vector3.Cross(U, V). normalized;
    }

    // Pomocná metoda pro změnu navazující hrany
    private void changeNextEdge(int edgeIndex, int nextEdgeIndex)
    {
        HalfEdgeEdge e = this.edges[edgeIndex];
        e.nextEdgeIndex = nextEdgeIndex;
        this.edges[edgeIndex] = e;
    }

    // Pomocná metoda pro změnu hrany korespondující ke stěně
    private void changeFaceEdge(int faceIndex, int edgeIndex)
    {
        HalfEdgeFace f = this.faces[faceIndex];
        f.edgeIndex = edgeIndex;
        this.faces[faceIndex] = f;
    }
    
    // Pomocná metoda pro změnu vrcholu ze kterého hrana vychází
    private void changeEdgeVertex(int edgeIndex, int fromVertexIndex)
    {
        HalfEdgeEdge e = this.edges[edgeIndex];
        e.fromVertexIndex = fromVertexIndex;
        this.edges[edgeIndex] = e;
    }

    // Pomocná metoda pro změnu navazující hrany
    private void changeEdgeFace(int edgeIndex, int faceIndex)
    {
        HalfEdgeEdge e = this.edges[edgeIndex];
        e.faceIndex = faceIndex;
        this.edges[edgeIndex] = e;
    }

    // Pomocná metoda pro změnu polohrany vycházející z vrcholu
    private void changeVertexEdge(int vertexIndex, int halfEdgeIndex)
    {
        HalfEdgeVertex v = this.vertices[vertexIndex];
        v.halfEdgeIndex = halfEdgeIndex;
        this.vertices[vertexIndex] = v;
    }

    // Pomocná metoda pro nastavení vycházející polohrany z vrcholu
    private void setOutGoingEdge(int vertexIndex, int edgeIndex)
    {
        if (this.vertices[vertexIndex].halfEdgeIndex == -1)
        {
            changeVertexEdge(vertexIndex, edgeIndex);
        }
    }

    // Pomocná metoda pro debug
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

    // Metoda pro zjištění všech vecholů jedné stěny
    public FaceVertexIndices FVQuery(int faceIndex)
    {
        HalfEdgeFace hef = this.faces[faceIndex];
        HalfEdgeEdge he0 = this.edges[hef.edgeIndex];
        HalfEdgeEdge he1 = this.edges[he0.nextEdgeIndex];
        HalfEdgeEdge he2 = this.edges[he1.nextEdgeIndex];

        return new FaceVertexIndices(he0.fromVertexIndex, he1.fromVertexIndex, he2.fromVertexIndex);
    }

    // Metoda pro získání sousedních vrcholů zadaného vrcholu
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
            // Vrchol na konci aktuální polohrany je sousedem zadaného vrcholu
            int neighborIndex = this.edges[this.edges[currEdge].nextEdgeIndex].fromVertexIndex;
            neighboringVertices.Add(neighborIndex);

            // Najdeme poslední polohranu trojúhelníku, která končí v procházeném vrcholu
            int next = this.edges[currEdge].nextEdgeIndex;
            int prev = this.edges[next].nextEdgeIndex;

            // Přes její dvojici přejdeme do sousedního trojúhelníku
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

    // Metoda pro získání sousedů napojených polohranami s nenulovou váhou ostrosti
    public (List<int>, float) VVQueryInsideVertexCreaseNeibs(int vertexIndex)
    {
        List<int> neighboringVertices = new List<int>();
        float maxCrease = 0f;

        int startEdge = this.vertices[vertexIndex].halfEdgeIndex;
        if (startEdge == -1)
        {
            return (neighboringVertices, -1);
        }

        int currEdge = startEdge;

        do
        {
            // Soused na konci polohrany se přidá pouze při nenulové ostrosti
            int neib = this.edges[this.edges[currEdge].nextEdgeIndex].fromVertexIndex;
            if(this.edges[currEdge].creaseWeight > 0)
            {
                maxCrease = Math.Max(maxCrease, this.edges[currEdge].creaseWeight);
                neighboringVertices.Add(neib);
            }

            // Obíhání pokračuje do sousedního trojúhelníku přes dvojici poslední polohrany
            int nextNeibEdge = this.edges[currEdge].nextEdgeIndex;
            int toCenterEdge = this.edges[nextNeibEdge].nextEdgeIndex;
            currEdge = this.edges[toCenterEdge].twinEdgeIndex;
        }
        while(currEdge != startEdge);

        return (neighboringVertices, maxCrease);
    }

    // Pomocná metoda pro výpis sousedních vrcholů do konzole
    public void printVerNeiborhood(int vertexIndex)
    {
        List<int> neighbors = VVQuery(vertexIndex);
        string neighborStr = string.Join(", ", neighbors);
    }

    // Metoda pro převod pozic vrcholů sítě na pole
    public Vector3[] getVertices()
    {
        Vector3[] v = new Vector3[this.vertices.Count];

        for(int i = 0; i < this.vertices.Count; i++)
        {
            v[i] = this.vertices[i].position;
        }

        return v;
    }

    // Metoda pro převod indexů stěn sítě na pole
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

    // Metoda pro rozdělení každého trojúhelníku pomocí nového středového vrcholu
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

            // Středový vrchol vznikne jako průměr vrcholů původního trojúhelníku
            this.vertices.Add(new HalfEdgeVertex(vNew, h02i, false));
            int newVertexIndex = this.vertices.Count - 1;

            // Přidání šesti nových polohran pro tři vznikající trojúhelníky
            this.edges.Add(new HalfEdgeEdge(p1, t0, h02i, h12i, false, 0f));
            this.edges.Add(new HalfEdgeEdge(newVertexIndex, t1, h10i, h01i, false, 0f));
            this.edges.Add(new HalfEdgeEdge(p2, t1, h12i, h22i, false, 0f));
            this.edges.Add(new HalfEdgeEdge(newVertexIndex, t2, h20i, h11i, false, 0f));
            this.edges.Add(new HalfEdgeEdge(p0, t2, h22i, h02i, false, 0f));
            this.edges.Add(new HalfEdgeEdge(newVertexIndex, t0, h00i, h21i, false, 0f));

            // Úprava původních polohran a přiřazení nových stěn
            changeNextEdge(h00i, h01i);
            changeNextEdge(h10i, h11i);
            changeEdgeFace(h10i, t1);
            changeNextEdge(h20i, h21i);
            changeEdgeFace(h20i, t2);

            this.faces.Add(new HalfEdgeFace(h10i));
            this.faces.Add(new HalfEdgeFace(h20i));
        }

        // Hraniční vrcholy se navážou na hrany ležící na okraji sítě
        for (int e = 0; e < this.edges.Count; e++)
        {
            if (this.edges[e].twinEdgeIndex == -1)
            {
                int vIdx = this.edges[e].fromVertexIndex;
                changeVertexEdge(vIdx, e);
            }
        }
    }

    // Metoda pro vyhlazení původních vrcholů sítě
    private void smooth(float creaseWeight, bool nonLinearBoundarySmoothing)
    {
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
                // Na hranici se pozice interpoluje mezi původním bodem a vyhlazenou polohou
                Vector3 fixedPosition = this.vertices[i].position;
                
                // Lineární vyhlazení hranice využívá dva krajní sousedy a středový vrchol
                int leftNeighbor = neibs[0];
                int rightNeighbor = neibs[neibs.Count - 1];
                Vector3 boundarySmoothLinear = (1f / 6f) * this.vertices[leftNeighbor].position
                                    + (4f / 6f) * this.vertices[i].position
                                    + (1f / 6f) * this.vertices[rightNeighbor].position;
                
                // Nelineární vyhlazení zohledňuje vzdálenost sousedů od vrcholu
                Vector3 leftNeighborPos = this.vertices[leftNeighbor].position;
                Vector3 rightNeighborPos = this.vertices[rightNeighbor].position;
                Vector3 vertexPos = this.vertices[i].position;

                float leftDist = Vector3.Distance(leftNeighborPos, vertexPos);
                float rightDist = Vector3.Distance(rightNeighborPos, vertexPos);
                leftDist = MathF.Max(leftDist, 0.0001f);
                rightDist = MathF.Max(rightDist, 0.0001f);

                float weightLeft = 1f / leftDist;
                float weightRight = 1f / rightDist;
                float weightCenter = 2f;
                float totalW = weightLeft + weightRight + weightCenter;

                Vector3 boundarySmoothNonLinear = (weightLeft * leftNeighborPos + weightRight * rightNeighborPos + weightCenter * vertexPos) / totalW;

                // Váha ostrosti určuje míru zachování původní polohy na hranici
                if (nonLinearBoundarySmoothing)
                {
                    newPositions[i] = Vector3.Lerp(boundarySmoothNonLinear, fixedPosition, creaseWeight);
                }
                else
                {
                    newPositions[i] = Vector3.Lerp(boundarySmoothLinear, fixedPosition, creaseWeight);
                }
            }
            else
            {
                // Vnitřní vrchol se vyhladí podle všech sousedních vrcholů
                Vector3 sum = Vector3.zero;
                for (int j = 0; j < neibs.Count; j++)
                {
                    sum += this.vertices[neibs[j]].position;
                }
                int n = neibs.Count;
                float alpha = (4f - 2f * Mathf.Cos((2f * Mathf.PI) / n)) / 9f;
                Vector3 smoothPosition = (1f - alpha) * this.vertices[i].position + (alpha / n) * sum;

                List<int> creaseNeighbors;
                float maxVertexCrease;
                (creaseNeighbors, maxVertexCrease) = VVQueryInsideVertexCreaseNeibs(i);

                if(creaseNeighbors.Count == 2)
                {
                    // Vrchol na ostré hraně se vyhladí pouze podél této hrany
                    Vector3 creaseSmooth = (1f / 6f) * this.vertices[creaseNeighbors[0]].position
                                        + (4f / 6f) * this.vertices[i].position
                                        + (1f / 6f) * this.vertices[creaseNeighbors[1]].position;
                                        
                    newPositions[i] = Vector3.Lerp(smoothPosition, creaseSmooth, maxVertexCrease);
                }
                else if (creaseNeighbors.Count > 2 || creaseNeighbors.Count == 1)
                {
                    newPositions[i] = Vector3.Lerp(smoothPosition, this.vertices[i].position, maxVertexCrease);
                }
                else
                {
                    newPositions[i] = smoothPosition;
                }
            }
        }

        // Pozice se aktualizují až po výpočtu všech nových poloh
        for (int i = 0; i < this.vertices.Count; i++)
        {
            if (this.vertices[i].isOriginal)
            {
                HalfEdgeVertex v = this.vertices[i];
                v.oldPosition = v.position; 
                v.position = newPositions[i];
                this.vertices[i] = v;
            }
        }
    }

    // Metoda pro převrácení hran a rozdělení hranic podle nastavení
    private void flip(bool divideBorderTriangles)
    {
        int originalEdgeCount = this.edges.Count;

        for (int i = 0; i < originalEdgeCount; i++)
        {
            HalfEdgeEdge e = this.edges[i];

            // Nově vytvořené hrany a již zpracované dvojice hran se přeskočí
            if (!e.isOriginal || (e.twinEdgeIndex != -1 && i >= e.twinEdgeIndex)) continue;

            // Hraniční hrana se přeskočí, pokud je dělení hranic vypnuté
            if (e.twinEdgeIndex == -1 && !divideBorderTriangles) continue;

            // Hraniční hrana se rozdělí přidáním středu a nového trojúhelníku
            if (e.twinEdgeIndex == -1 && divideBorderTriangles)
            {
                int e0Index = i;
                int e1Index = this.edges[i].nextEdgeIndex;
                int e2Index = this.edges[e1Index].nextEdgeIndex;

                Vector3 borderVCord0 = this.vertices[e.fromVertexIndex].position;
                Vector3 borderVCord1 = this.vertices[this.edges[e1Index].fromVertexIndex].position;
                Vector3 newBorderVCord = (borderVCord0 + borderVCord1) / 2;
                int middleVertexIndex = this.edges[e2Index].fromVertexIndex;

                // Indexy nových polohran navazují na konec seznamu
                int ne0Index = this.edges.Count + 0;
                int ne1Index = this.edges.Count + 1;
                int ne2Index = this.edges.Count + 2;

                // Určení indexů nového vrcholu a stěny
                int nvIndex = this.vertices.Count;
                int nfIndex = this.faces.Count;
                int ofIndex = this.edges[e2Index].faceIndex;

                // Přidání nového trojúhelníku, vrcholu a jeho polohran
                this.faces.Add(new HalfEdgeFace(ne2Index));
                this.vertices.Add(new HalfEdgeVertex(newBorderVCord, ne0Index, false));

                this.edges.Add(new HalfEdgeEdge(nvIndex, nfIndex, e1Index, -1, false, 0f));
                this.edges.Add(new HalfEdgeEdge(nvIndex, ofIndex, e2Index, ne2Index, false, 0f));
                this.edges.Add(new HalfEdgeEdge(middleVertexIndex, nfIndex, ne0Index, ne1Index, false, 0f));

                // Aktualizace původní stěny a propojení jejích polohran
                changeFaceEdge(ofIndex, e2Index);
                changeEdgeFace(e1Index, nfIndex);
                changeNextEdge(e1Index, ne2Index);
                changeNextEdge(e0Index, ne1Index);

                continue;
            }

            // Hrana s nenulovou ostrostí se rozdělí a její ostrost se přenese na nové hrany
            if (e.creaseWeight > 0)
            {
                // Určení polohran obou sousedních trojúhelníků
                int abi = i;
                int bai = this.edges[abi].twinEdgeIndex;
                int bdi = this.edges[abi].nextEdgeIndex;
                int dai = this.edges[bdi].nextEdgeIndex;
                int aci = this.edges[bai].nextEdgeIndex;
                int cbi = this.edges[aci].nextEdgeIndex;

                // Určení vrcholů obou sousedních trojúhelníků
                int ai = this.edges[abi].fromVertexIndex;
                int bi = this.edges[bai].fromVertexIndex;
                int ci = this.edges[cbi].fromVertexIndex;
                int di = this.edges[dai].fromVertexIndex;

                // Rezervace indexů šesti nových polohran
                int mdi = this.edges.Count + 0;
                int dmi = this.edges.Count + 1;
                int mbi = this.edges.Count + 2;
                int bmi = this.edges.Count + 3;
                int mci = this.edges.Count + 4;
                int cmi = this.edges.Count + 5;

                // Nový vrchol leží uprostřed rozdělované hrany
                int mi = this.vertices.Count;
                Vector3 nvp = (this.vertices[ai].position + this.vertices[bi].position) / 2;

                // Určení původních a nových stěn
                int ot0i = this.edges[dai].faceIndex;
                int ot1i = this.edges[aci].faceIndex;
                int nt0i = this.faces.Count + 0;
                int nt1i = this.faces.Count + 1;

                // Nové hrany navazující na původní ostrou hranu zachovají její váhu
                float ec = this.edges[abi].creaseWeight;

                // Přidání polohran upravených a nově vytvořených trojúhelníků
                this.edges.Add(new HalfEdgeEdge(mi, ot0i, dai, dmi, false, 0f));
                this.edges.Add(new HalfEdgeEdge(di, nt0i, mbi, mdi, false, 0f));
                this.edges.Add(new HalfEdgeEdge(mi, nt0i, bdi, bmi, false, ec));
                this.edges.Add(new HalfEdgeEdge(bi, nt1i, mci, mbi, false, ec));
                this.edges.Add(new HalfEdgeEdge(mi, nt1i, cbi, cmi, false, 0f));
                this.edges.Add(new HalfEdgeEdge(ci, ot1i, bai, mci, false, 0f));

                // Přidání středového vrcholu a dvou nových stěn
                this.vertices.Add(new HalfEdgeVertex(nvp, mbi, false));

                this.faces.Add(new HalfEdgeFace(mbi));
                this.faces.Add(new HalfEdgeFace(bmi));

                // Aktualizace návazností polohran a příslušnosti ke stěnám
                changeNextEdge(bdi, dmi);
                changeNextEdge(abi, mdi);
                changeNextEdge(aci, cmi);
                changeNextEdge(cbi, bmi);

                changeFaceEdge(ot0i, abi);
                changeFaceEdge(ot1i, bai);

                changeEdgeFace(bdi, nt0i);
                changeEdgeFace(cbi, nt1i);

                changeEdgeVertex(bai, mi);

                // Referenční hrana vrcholu bi musí stále z tohoto vrcholu vycházet
                changeVertexEdge(bi, bmi);
            }
            else
            {
                int f00i = i;
                int f01i = this.edges[i].nextEdgeIndex;
                int f02i = this.edges[f01i].nextEdgeIndex;
                int f10i = this.edges[f00i].twinEdgeIndex;
                int f11i = this.edges[f10i].nextEdgeIndex;
                int f12i = this.edges[f11i].nextEdgeIndex;

                int oldV0 = e.fromVertexIndex;
                int oldV1 = this.edges[f10i].fromVertexIndex;

                // Nové počáteční vrcholy převrácené dvojice polohran
                int nf00fvi = this.edges[f12i].fromVertexIndex;
                int nf10fvi = this.edges[f02i].fromVertexIndex;

                // Stěny zůstávají zachovány, mění se jejich hraniční polohrany
                int nf00fi = this.edges[f00i].faceIndex;
                int nf10fi = this.edges[f10i].faceIndex;

                HalfEdgeEdge nf00 = new HalfEdgeEdge(nf00fvi, nf00fi, f02i, f10i, false, 0f);
                HalfEdgeEdge nf10 = new HalfEdgeEdge(nf10fvi, nf10fi, f12i, f00i, false, 0f);
                this.edges[f00i] = nf00;
                this.edges[f10i] = nf10;

                // Přepojení polohran v obou sousedních trojúhelnících
                changeNextEdge(f02i, f11i);
                changeNextEdge(f11i, f00i);
                changeEdgeFace(f11i, nf00fi);
                changeNextEdge(f12i, f01i);
                changeNextEdge(f01i, f10i);
                changeEdgeFace(f01i, nf10fi);

                // Aktualizace referenčních polohran stěn a vrcholů
                changeFaceEdge(nf00fi, f00i);
                changeFaceEdge(nf10fi, f10i);

                changeVertexEdge(oldV0, f11i);
                changeVertexEdge(oldV1, f01i);
            }
        }

        // Hraniční vrcholy se znovu navážou na polohrany ležící na okraji sítě
        for (int eIdx = 0; eIdx < this.edges.Count; eIdx++)
        {
            if (this.edges[eIdx].twinEdgeIndex == -1)
            {
                int vIdx = this.edges[eIdx].fromVertexIndex;
                changeVertexEdge(vIdx, eIdx);
            }
        }
    }

    // Metoda pro označení všech vrcholů a polohran jako původních pro další iteraci
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

    // Metoda pro spuštění jedné iterace sqrt(3) subdivize
    public void subDivide(bool divideBorderTriangles, float creaseWeight, bool nonLinearBoundarySmoothing)
    {
        this.smooth(creaseWeight, nonLinearBoundarySmoothing);
        this.split();
        this.flip(divideBorderTriangles);
        this.resetOriginalFlags();
    }
}