using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class PGSSpline : MonoBehaviour
{
    private LineRenderer line;

    [Header("Manual Mode")]
    public Transform[] controlPoints;

    [Header("Procedural Mode Settings")]
    public Transform startPoint;
    public Transform endPoint;
    public bool useProceduralPoints = true;
    public Vector3[] generatedPoints;

    [Header("Catmull Rom Spline Settings")]
    [Range(2, 100)] public int splinePointCount = 4;
    [Range(1, 5)] public int segmentResolution = 1; //Resolution per each "catmull curve segment"
    [Range(1, 3)] public float splineLengthMultiplier = 1f;
    const int denseSamplesPerCurve = 32; // good default

    [Header("Spline Curvature Settings")]
    [Range(-10, 10f)] public float horizontalCurvature = 0f;
    [Range(-10, 10f)] public float verticalCurvature = 0f;
    [Range(0, 1)] public float CurvatureArcWeightInfluence = 1f;

    [Header("Spiral Settings")]
    public bool useSpiral = false;
    [Range(0, 5f)] public float spiralRadius = 1f;
    [Range(0, 10f)] public float spiralTurns = 1f;

    [Header("Perlin Noise Settings")]
    public bool usePerlinNoise = false;
    public int seed = 11111;
    [Range(0f, 5f)] public float noiseAmplitude = 1f;
    [Range(0.1f, 10f)] public float noiseFrequency = 1f;
    [Range(0f, 2f)] public float noiseVerticalBias = 1f;

    [Header("Target Bend")]
    public bool useTargetBend = true;
    public bool useEvenSpacing = true;
    public bool bendAway = true;
    [Range(0f, 20f)] public float bendRadius = 5f;
    [Range(0f, 5f)] public float bendStrength = 1f;
    public Transform bendTarget;
    private const float BEND_EPSILON = 0.001f;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        if (useProceduralPoints)
        {
            GenerateControlPointsFromAtoB();
            DrawCatmullRomProceduralSpline(generatedPoints);
        }
        else
        {
            if (controlPoints != null && controlPoints.Length >= 4) DrawCatmullRomFromTransforms(controlPoints);
        }
    }

    private void GenerateControlPointsFromAtoB()
    {
        if (startPoint == null || endPoint == null)
            return;

        Vector3 A = startPoint.position;
        Vector3 B = endPoint.position;
        Vector3 AB = B - A;
        Vector3 dir = AB.normalized;
        Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;  // Sideways direction (perpendicular to AB, using world up)  
        Vector3 up = Vector3.Cross(side, dir).normalized;
        float distance = AB.magnitude;

        //Build array of points: A/Start + internal/spline points + B/End
        int totalPointsLength = splinePointCount + 2;  
        generatedPoints = new Vector3[totalPointsLength];
        generatedPoints[0] = A;
        generatedPoints[totalPointsLength - 1] = B;

        //Extending the line AB based on pathLengthMultiplier
        float extra = distance * (splineLengthMultiplier - 1f) * 0.5f;
        Vector3 extendedA = A - dir * extra;
        Vector3 extendedB = B + dir * extra;
        float extendedDistance = (extendedB - extendedA).magnitude;

        //Positioning the internal spline points between A and B
        for (int p = 1; p <= totalPointsLength - 2; p++) 
        {
            //Initial point position
            float t = p / (float)(totalPointsLength - 1);
            Vector3 onLine = extendedA + dir * (extendedDistance * t);

            //Curvature logic
            float arcWeightFactor = Mathf.Sin(Mathf.PI * t) * CurvatureArcWeightInfluence;
            Vector3 curvatureOffset = (side * horizontalCurvature * arcWeightFactor) + (Vector3.up * verticalCurvature * arcWeightFactor);

            //Spiral logic
            Vector3 spiralOffset = Vector3.zero;
            Vector3 radialDir = side;
            if (useSpiral)
            {
                float angle = t * Mathf.PI * 2f * spiralTurns;
                radialDir = (side * Mathf.Cos(angle)) + (Vector3.up * Mathf.Sin(angle));
                float radius = spiralRadius * arcWeightFactor; 
                spiralOffset = radialDir * radius;
            }

            //Perlin Noise logic : REFACTOR TO COMPLETE NEW METHOD
            Vector3 noiseOffset = Vector3.zero;
            if (usePerlinNoise) 
            {
                Vector3 binormal = Vector3.Cross(dir, radialDir).normalized;

                float baseNoise = t * noiseFrequency + seed * 0.123f;
                float nSide = Mathf.PerlinNoise(baseNoise, 0f) ;
                float nUp = Mathf.PerlinNoise(baseNoise, 37.17f);

                float sideVal = (nSide - 0.5f) * 2f;
                float upVal = (nUp - 0.5f) * 2f;

                float radius = noiseAmplitude * arcWeightFactor;
                float sidewayNoise = noiseAmplitude * arcWeightFactor * noiseVerticalBias;

                noiseOffset = (side * sideVal * radius) + (binormal* upVal * radius);
            }

            //LEARN: Target/Object Based Bending Logic
            Vector3 targetOffset = Vector3.zero;
            if (useTargetBend && bendTarget != null) 
            {
                //ASK: basePose here adds up everything, so this means the bend should always be calculated last? Is this intended?
                //TODO: If object is practically supposed to touch the spline, exaggerated the bend a little since the current logic lead to bending through the object.
                //TODO: If both are at the same height, choose a random up or down dir to bend towards/away from
                Vector3 basePose = onLine + curvatureOffset + spiralOffset + noiseOffset;
                Vector3 toTarget = bendTarget.position - basePose;
                float distToTarget = toTarget.magnitude;

                if (distToTarget < bendRadius) 
                {
                    float safeDist = Mathf.Max(distToTarget, BEND_EPSILON);

                    Vector3 dirToTarget = toTarget / safeDist;
                    if(bendAway) dirToTarget = -dirToTarget;

                    float bend = 1f - (distToTarget / bendRadius);
                    float weight = bend * bend;

                    weight *= arcWeightFactor;
                    targetOffset = dirToTarget * bendStrength * weight;
                }
            }

            generatedPoints[p] = onLine + curvatureOffset + spiralOffset + noiseOffset + targetOffset;
        }


        if (useEvenSpacing && generatedPoints != null)
        {
            RedistributePointsEvenlyAlongCurve(ref generatedPoints);
        }
    }

    private void DrawCatmullRomProceduralSpline(Vector3[] points)
    {
        int totalCatmullCurves = points.Length - 3;
        int totalSegments = totalCatmullCurves * segmentResolution;
        line.positionCount = totalSegments + 1;

        //Iterating through points and storing positions of each point per each curve segment 
        int index = 0;
        for (int i = 0; i < totalCatmullCurves; i++)
        {
            Vector3 p0 = points[i];
            Vector3 p1 = points[i + 1];
            Vector3 p2 = points[i + 2];
            Vector3 p3 = points[i + 3];

            //Evaluate t on each catmull's curve segment based on total segment/resolution count per each curve segment
            for (int s = 0; s < segmentResolution; s++)
            {
                float t = s / (float)segmentResolution;
                line.SetPosition(index, CatmullRom(p0, p1, p2, p3, t));
                index++;
            }
        }

        Vector3 lp0 = points[points.Length - 4];
        Vector3 lp1 = points[points.Length - 3];
        Vector3 lp2 = points[points.Length - 2];
        Vector3 lp3 = points[points.Length - 1];

        line.SetPosition(index, CatmullRom(lp0, lp1, lp2, lp3, 1f));
    }

    private void RedistributePointsEvenlyAlongCurve(ref Vector3[] points)
    {
        int pointCount = points.Length;
        if (pointCount < 4)
            return;

        // ---------- STEP 1: Dense samples along current Catmull curve ----------
        const int denseSamplesPerCurve = 32; // per Catmull segment
        List<Vector3> densePoints = new List<Vector3>();

        int numCurves = pointCount - 3;

        for (int i = 0; i < numCurves; i++)
        {
            Vector3 p0 = points[i];
            Vector3 p1 = points[i + 1];
            Vector3 p2 = points[i + 2];
            Vector3 p3 = points[i + 3];

            for (int s = 0; s <= denseSamplesPerCurve; s++)
            {
                float t = s / (float)denseSamplesPerCurve;
                Vector3 pos = CatmullRom(p0, p1, p2, p3, t);

                // Avoid duplicating the first sample of each segment except the first one
                if (i > 0 && s == 0)
                    continue;

                densePoints.Add(pos);
            }
        }

        int denseCount = densePoints.Count;
        if (denseCount < 2)
            return;

        // ---------- STEP 2: Cumulative distances ----------
        float[] cumLen = new float[denseCount];
        cumLen[0] = 0f;

        for (int i = 1; i < denseCount; i++)
        {
            float segLen = Vector3.Distance(densePoints[i], densePoints[i - 1]);
            cumLen[i] = cumLen[i - 1] + segLen;
        }

        float totalLen = cumLen[denseCount - 1];
        if (totalLen <= 0f)
            return;

        // ---------- STEP 3: New points at equal distance intervals ----------
        Vector3[] newPoints = new Vector3[pointCount];
        int denseIndex = 0;

        for (int j = 0; j < pointCount; j++)
        {
            float targetDist = (j / (float)(pointCount - 1)) * totalLen;

            // move denseIndex forward until we bracket targetDist
            while (denseIndex < denseCount - 2 && cumLen[denseIndex + 1] < targetDist)
            {
                denseIndex++;
            }

            float segmentStart = cumLen[denseIndex];
            float segmentEnd = cumLen[denseIndex + 1];
            float segmentLen = segmentEnd - segmentStart;

            float u = 0f;
            if (segmentLen > 0f)
                u = (targetDist - segmentStart) / segmentLen;

            Vector3 a = densePoints[denseIndex];
            Vector3 b = densePoints[denseIndex + 1];

            newPoints[j] = Vector3.Lerp(a, b, u);
        }

        points = newPoints;
    }

    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        // Calculate the Catmull-Rom spline point at parameter t
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * ((2f * p1) +
                       (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private void DrawCatmullRomFromTransforms(Transform[] tPoints)
    {
        //Convert transform into position Vector3 and pass it to ProceduralSplineDrawer / DrawCatmullRomVector3Array
        Vector3[] points = new Vector3[tPoints.Length];
        for (int i = 0; i < tPoints.Length; i++)
        {
            points[i] = tPoints[i].position;
        }

        DrawCatmullRomProceduralSpline(points);
    }

    //TODO: REFACTOR THIS
    private void OnDrawGizmos()
    {
        // Decide which set of points to visualize
        Vector3[] pts = null;

        if (useProceduralPoints)
        {
            // In the editor, GenerateControlPointsFromAtoB can still run,
            // because it only uses Transform positions.
            if (startPoint == null || endPoint == null)
                return;

            GenerateControlPointsFromAtoB();
            pts = generatedPoints;
        }
        else
        {
            if (controlPoints == null || controlPoints.Length < 4)
                return;

            pts = new Vector3[controlPoints.Length];
            for (int i = 0; i < controlPoints.Length; i++)
            {
                if (controlPoints[i] != null)
                    pts[i] = controlPoints[i].position;
            }
        }

        if (pts == null || pts.Length < 4)
            return;
        
        //Draw spheres at control points
        Gizmos.color = Color.yellow;
        float sphereRadius = 0.065f;

        for (int i = 0; i < pts.Length; i++)
        {
            Gizmos.DrawSphere(pts[i], sphereRadius);
        }

        Gizmos.color = Color.red; 
        Gizmos.DrawSphere(pts[0], sphereRadius * 1.2f);   // p0
        Gizmos.color = Color.green; 
        Gizmos.DrawSphere(pts[1], sphereRadius * 1.2f);   // p1
        int lastP = pts.Length - 2;
        Gizmos.color = Color.green; 
        Gizmos.DrawSphere(pts[lastP], sphereRadius * 1.2f);   // pN - 2
        int endP = pts.Length - 1;
        Gizmos.color = Color.red; 
        Gizmos.DrawSphere(pts[endP], sphereRadius * 1.2f);   // pN - 1

        //Spline Color
        Gizmos.color = Color.white;
        int numCurves = pts.Length - 3;
        if (numCurves <= 0) return;

        Vector3 previousPos = CatmullRom(pts[0], pts[1], pts[2], pts[3], 0f);

        for (int i = 0; i < numCurves; i++)
        {
            Vector3 p0 = pts[i];
            Vector3 p1 = pts[i + 1];
            Vector3 p2 = pts[i + 2];
            Vector3 p3 = pts[i + 3];

            for (int s = 1; s <= segmentResolution; s++)
            {
                float t = s / (float)segmentResolution;
                Vector3 pos = CatmullRom(p0, p1, p2, p3, t);

                Gizmos.DrawLine(previousPos, pos);
                previousPos = pos;
            }
        }
    }
}
