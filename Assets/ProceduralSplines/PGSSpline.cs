using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Rendering;

[RequireComponent(typeof(LineRenderer))]
public class PGSSpline : MonoBehaviour
{
    [Header("Handles Points LSystems")]
    public List<Transform> handles;
    public Vector3[] generatedPoints;
    public List<LSystems> generatedLSystems = new List<LSystems>();


    [Header("Catmull Rom Spline Settings")]
    [Range(10, 400)] public int totalSplinePoints = 20;
    [Range(1, 5)] public int segmentResolution = 1; //Resolution per each "catmull curve segment"
    [Range(1, 3)] public float splineLengthMultiplier = 1f;
    public bool loopedSpline = false;

    [Header("Spline Curvature Settings")]
    [Range(-10, 10f)] public float horizontalCurvature = 0f;
    [Range(-10, 10f)] public float verticalCurvature = 0f;

    [Header("Spiral Settings")]
    public bool enableSpiral = false;
    [Range(0f, 5f)] public float spiralRadius = 1f;
    [Range(0f, 10f)] public float spiralTurns = 1f;

    [Header("Perlin Noise Settings")]
    public bool enableNoise = false;
    [Range(0.01f, 2f)] public float noiseAmplitude = 1f;
    [Range(0.001f, 1f)] public float noiseFrequency = 1f;
    [Range(-100f, 100f)] public float perlinOffset = 0f;
    [Range(0.25f, 4f)] public float noiseSharpness = 1f;

    [Header("Bend Setting")]
    public bool enableBending = true;
    public bool useEvenSpacing = true;
    public bool bendAway = true;
    public LayerMask bendLayer = 3;
    [Range(1f, 20f)] public float bendRadius = 5f;
    [Range(1f, 5f)] public float bendStrength = 1f;
    private const int maxPhysicsTargetsPerPoint = 4;
    private const float bendClamp = 0.01f;

    [Header("Advanced Curve Setting")]
    public bool useWeightFallOff = false;
    [Range(0, 1)] public float falloffInfluence = 1f;

    [Header("L-SYSTEM Settings")]
    public bool randomPosition = true;
    [Range(1, 6)] public int lSystemCount = 1;
    public LSystems lSystemPrefab;

    [Header("Gizmos Settings")]
    public bool enableGizmoAxes = false;
    [Range(0.1f, 1f)] public float gizmoAxesLength = 1f;
    public bool enableGizmoPoints = false;
    [Range(0.01f, 0.1f)] public float gizmoSphereRadius = 0.065f;

    private LineRenderer line;

    //CONSIDER: Objects that will always attract or repel

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
    }

    private void Start()
    {
        SpawnLSystemOnSpline();
    }

    private void Update()
    {
        BuildSplineFromHandles();
        LineRenderOnCatmullRomPGS(generatedPoints); 
    }

    private void BuildSplineFromHandles()
    {
        if (handles == null || handles.Count == 0)
        {
            generatedPoints = null;
            return;
        }

        //CASE 1 - Single handle spline gen
        if (handles.Count == 1)
        {
            Vector3 A = handles[0].position;
            Vector3 forwardA = handles[0].forward;
            Vector3 B = A + forwardA.normalized * 20f;

            generatedPoints = GenerateSplinePointsFromAtoB(A, B, totalSplinePoints);

            //Fix for bug where points were distanced unevenly due to bend influence from scene objects
            if (enableBending && useEvenSpacing && generatedPoints != null)
            {
                RedistributePointsEvenlyAlongCurve(ref generatedPoints);
            }

            return;
        }

        //CASE 2 - Two or more handles
        int splineSectionCount = loopedSpline ? handles.Count : handles.Count - 1; //Looped = 1 extra spline section from last handle to first handle

        //2.1 Measure each spline section's length
        float[] lengthOfSplineSection = new float[splineSectionCount];
        float totalSplineLength = 0f;

        if (loopedSpline)
        {
            for (int i = 0; i < splineSectionCount; i++)
            {
                int indexA = i;
                int indexB = (i + 1) % handles.Count;

                Vector3 handleA = handles[indexA].position;
                Vector3 handleB = handles[indexB].position;

                float currentSectionLength = Vector3.Distance(handleA, handleB);
                lengthOfSplineSection[i] = currentSectionLength;
                totalSplineLength += currentSectionLength;
            }
        }
        else //Not looped?
        {
            for (int i = 0; i < splineSectionCount; i++)
            {
                int indexA = i;
                int indexB = i + 1;

                Vector3 handleA = handles[indexA].position;
                Vector3 handleB = handles[indexB].position;

                float currentSectionLength = Vector3.Distance(handleA, handleB);
                lengthOfSplineSection[i] = currentSectionLength;
                totalSplineLength += currentSectionLength;
            }
        }

        //2.2 Decide how many spline/internal points each spline section gets
        int[] internalPointsPerSection = new int[splineSectionCount];
        int remainingInternal = totalSplinePoints;
        float remainingLength = totalSplineLength;

        for (int i = 0; i < splineSectionCount; i++)
        {
            // splineSecCount - 1 = Last spline section, and last spline section gets whatever points are left
            if (i == splineSectionCount - 1)
            {
                internalPointsPerSection[i] = remainingInternal;
            }
            else
            {
                float share = (remainingLength > 0f) ? (lengthOfSplineSection[i] / remainingLength) : 0f;
                int allocated = Mathf.RoundToInt(remainingInternal * share);

                if (allocated < 0) allocated = 0;

                internalPointsPerSection[i] = allocated;
                remainingInternal -= allocated;
                remainingLength -= lengthOfSplineSection[i];
            }
        }

        //2.3 Generate points for each spline section between handleN - handleN+1 and combine them
        List<Vector3> combinedPoints = new List<Vector3>(); //Lists cuz using array here were pain
        for (int i = 0; i < splineSectionCount; i++)
        {
            int indexA;
            int indexB;

            if (loopedSpline)
            {
                indexA = i;
                indexB = (i + 1) % handles.Count;
            }
            else
            {
                indexA = i;
                indexB = i + 1;
            }

            Vector3 A = handles[indexA].position;
            Vector3 B = handles[indexB].position;
            int currentSectionsInternalCount = internalPointsPerSection[i];
            Vector3[] sectionPoints = GenerateSplinePointsFromAtoB(A, B, currentSectionsInternalCount);

            if (i == 0)
            {
                //First section: keep everything (A point + internals + B point)
                combinedPoints.AddRange(sectionPoints);
            }
            else
            {
                //Later sections: skip the first point A since its same as previous section's B || end point
                for (int j = 1; j < sectionPoints.Length; j++)
                {
                    combinedPoints.Add(sectionPoints[j]);
                }
            }
        }

        generatedPoints = combinedPoints.ToArray();

        //Fix for bug where points were distanced unevenly due to bend influence from scene objects
        if (enableBending && useEvenSpacing && generatedPoints != null)
        {
            RedistributePointsEvenlyAlongCurve(ref generatedPoints);
        }
    }

    private Vector3[] GenerateSplinePointsFromAtoB(Vector3 A, Vector3 B, int internalPointCount)
    {
        Vector3 AB = B - A;
        Vector3 dir = AB.normalized;
        Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;  // Sideways direction  
        Vector3 up = Vector3.Cross(side, dir).normalized; //Local up per point
        float distance = AB.magnitude;

        //1 - Make array for points: A/Start + internal/spline points + B/End
        int totalPointsLength = internalPointCount + 2;
        Vector3[] points = new Vector3[totalPointsLength];
        points[0] = A;
        points[totalPointsLength - 1] = B;

        //2 - Extending the line AB based on pathLengthMultiplier
        float extra = distance * (splineLengthMultiplier - 1f) * 0.5f;
        Vector3 extendedA = A - dir * extra;
        Vector3 extendedB = B + dir * extra;
        float extendedDistance = (extendedB - extendedA).magnitude;

        //3 - Positioning the internal spline points between A and B. Looping from first internal to last internal point.
        //pt 1 for first internal point and totalPts - 2 to get the final internal point instead the actual end point
        for (int p = 1; p <= totalPointsLength - 2; p++) 
        {
            //INTIAL POSITION OF THE POINT
            float t = p / (float)(totalPointsLength - 1);
            Vector3 currentPointOnLine = extendedA + dir * (extendedDistance * t);

            //CURVATURE LOGIC
            float arcWeightFactor = useWeightFallOff? Mathf.Sin(Mathf.PI * t) * falloffInfluence : 1f; //Sin90 = 0
            Vector3 curvatureOffset = (side * horizontalCurvature * arcWeightFactor) + (up * verticalCurvature * arcWeightFactor);

            //SPIRAL LOGIC
            Vector3 spiralOffset = Vector3.zero;
            if (enableSpiral)
            {
                Vector3 radialDir = side;
                float angle = t * Mathf.PI * 2f * spiralTurns;
                radialDir = (side * Mathf.Cos(angle)) + (Vector3.up * Mathf.Sin(angle));
                float radius = spiralRadius * arcWeightFactor; 
                spiralOffset = radialDir * radius;
            }

            //PERLIN NOISE LOGIC
            Vector3 noiseOffset = Vector3.zero;
            if (enableNoise) 
            {
                float noiseBase = (p + perlinOffset) * noiseFrequency;
                float sideNoise = Mathf.PerlinNoise(noiseBase, 0f);
                float upNoise = Mathf.PerlinNoise(-sideNoise, 0f);

                //Remmap side and up noise to -1, 1 for directional functionality
                float sideValue = (sideNoise - 0.5f) * 2f;
                float upValue = (upNoise - 0.5f) * 2f;

                //Sharpen noise to get spikyness
                sideValue = Mathf.Sign(sideValue) * Mathf.Pow(Mathf.Abs(sideValue), noiseSharpness);
                upValue = Mathf.Sign(upValue) * Mathf.Pow(Mathf.Abs(upValue), noiseSharpness);


                noiseOffset = (side * sideValue * noiseAmplitude) + (up * upValue * noiseAmplitude);
            }

            //Final offset position of current point before doing bending logic
            Vector3 basePos = currentPointOnLine + curvatureOffset + spiralOffset + noiseOffset;

            //BENDING TO AVOID OR BE ATTRACTED LOGIC
            Vector3 targetOffset = Vector3.zero;
            if (enableBending) 
            {
                Collider[] cols = Physics.OverlapSphere(basePos, bendRadius, bendLayer);
                int maxCollisions = Mathf.Min(cols.Length, maxPhysicsTargetsPerPoint); //Max objects influecing the spline

                for (int i = 0; i < maxCollisions; i++)
                {
                    Collider currentCol = cols[i];
                    targetOffset += ComputeColliderBendOffset(basePos, currentCol, arcWeightFactor, A, B);
                }
            }

            points[p] = basePos + targetOffset;
        }

        return points;
    }

    private Vector3 ComputeColliderBendOffset(Vector3 splinePoint, Collider col, float archWeightFactor, Vector3 A, Vector3 B)
    {
        if (col == null) 
        {
            Debug.LogWarning("Collider passed into ComputeColliderBendOffset is null!");
            return Vector3.zero;
        }

        //1 - Gets closest point on object's surface
        Vector3 closest = col.ClosestPoint(splinePoint);
        Vector3 toTarget = closest - splinePoint;
        float distanceToTarget = toTarget.magnitude;
        if (distanceToTarget >= bendRadius) return Vector3.zero;

        //2 - Choosing bend direction
        Vector3 bendDir;

        if (distanceToTarget > bendClamp)
        {
            //Case 1: Collider is not intersecting the spline
            bendDir = toTarget / distanceToTarget;
        }
        else 
        {
            //Case 2: Object colider intersects the AB line, so force a sideways bend dir based on which side the object came from
            Vector3 ABdir = (B - A).normalized;
            Vector3 objCenter = col.bounds.center;
            //WHAT:
            float t = Vector3.Dot(objCenter - A, ABdir);
            Vector3 linePoint = A + ABdir * t;
            Vector3 fromLine = objCenter - linePoint;
            if (fromLine.sqrMagnitude < 0.0001f) fromLine = Vector3.Cross(ABdir, Vector3.up);
            bendDir = fromLine.normalized;
        }

        //3 - Choose to bend towards or away from
        if (bendAway) bendDir = -bendDir;

        //4 - 
        float bend = 1f - (distanceToTarget / bendRadius);
        float weight = bend * bend * archWeightFactor;

        return bendDir * bendStrength * weight;
    }

    private void RedistributePointsEvenlyAlongCurve(ref Vector3[] points)
    {
        //1 - Dense samples along current Catmull curve
        List<Vector3> revaluatedPoints = new List<Vector3>();
        int totalCatmullSegments = points.Length - 3;

        for (int i = 0; i < totalCatmullSegments; i++)
        {
            Vector3 p0 = points[i];
            Vector3 p1 = points[i + 1];
            Vector3 p2 = points[i + 2];
            Vector3 p3 = points[i + 3];

            for (int seg = 0; seg <= segmentResolution; seg++)
            {
                float t = seg / (float)segmentResolution;
                Vector3 pos = CatmullRom(p0, p1, p2, p3, t);

                // Avoid duplicating the first sample of each segment except the first one
                if (i > 0 && seg == 0) continue;

                revaluatedPoints.Add(pos);
            }
        }

        //2 - Cumulative distances
        float[] cumLen = new float[revaluatedPoints.Count];
        cumLen[0] = 0f;

        for (int i = 1; i < revaluatedPoints.Count; i++)
        {
            float segLen = Vector3.Distance(revaluatedPoints[i], revaluatedPoints[i - 1]);
            cumLen[i] = cumLen[i - 1] + segLen;
        }

        float totalLen = cumLen[revaluatedPoints.Count - 1];

        //3 - New points at equal distance intervals
        Vector3[] newPoints = new Vector3[points.Length];
        int denseIndex = 0;

        for (int i = 0; i < points.Length; i++)
        {
            float targetDist = (i / (float)(points.Length - 1)) * totalLen;

            while (denseIndex < revaluatedPoints.Count - 2 && cumLen[denseIndex + 1] < targetDist)
            {
                denseIndex++;
            }

            float segmentStart = cumLen[denseIndex];
            float segmentEnd = cumLen[denseIndex + 1];
            float segmentLen = segmentEnd - segmentStart;

            float u = 0f;
            if (segmentLen > 0f)
                u = (targetDist - segmentStart) / segmentLen;

            Vector3 a = revaluatedPoints[denseIndex];
            Vector3 b = revaluatedPoints[denseIndex + 1];

            newPoints[i] = Vector3.Lerp(a, b, u);
        }

        //Fix for bug that caused spline to have 1 segment missing at A and B
        //Since this is catmull curve, passing the internal points will cause it to redistribute from p2 rather than p1, hence why passing A and B
        Vector3 start = points[0]; //Old A
        Vector3 end = points[points.Length - 1]; //Old B
        points = newPoints;
        points[0] = start; //New A
        points[points.Length - 1] = end; //New B
    }

    private void LineRenderOnCatmullRomPGS(Vector3[] points)
    {
        //Deciding resolution/segement count of line renderer
        int totalCatmullSegments = points.Length - 3;
        int totalSegments = totalCatmullSegments * segmentResolution;
        line.positionCount = totalSegments + 1;

        int index = 0;
        Vector3 previousPos = CatmullRom(points[0], points[1], points[2], points[3], 0f);
        line.SetPosition(index, previousPos);
        index++;

        for (int i = 0; i < totalCatmullSegments; i++)
        {
            Vector3 p0 = points[i];
            Vector3 p1 = points[i + 1];
            Vector3 p2 = points[i + 2];
            Vector3 p3 = points[i + 3];

            //Evaluate t on each catmull's curve segment based on total segment/resolution count per each curve segment
            for (int lineSeg = 1; lineSeg <= segmentResolution; lineSeg++)
            {
                float t = lineSeg / (float)segmentResolution;
                Vector3 pos = CatmullRom(p0, p1, p2, p3, t);

                line.SetPosition(index, pos);
                index++;
                previousPos = pos;
            }
        }
    }

    //-------------------------------------====[[[[[[ L SYSTEM SPAWN ON SPLINE FUNCTION HERE]]]]]]====-------------------------------------
    private void SpawnLSystemOnSpline()
    {
        //Ensure list is refreshed every run
        foreach (LSystems currentLsys in generatedLSystems) 
        {
            if (currentLsys != null) DestroyImmediate(currentLsys.gameObject);
        }
        generatedLSystems.Clear();

        //Generate L systems on spline
        for (int i = 0; i < lSystemCount; i++) 
        {
            float t;
            if (randomPosition)
            {
                t = Random.Range(0.0f, 1.0f);
            }
            else 
            {
                t =  i / (float)(lSystemCount - 1);
            }

            Vector3 pos = EvalutePosition(t);
            pos = new Vector3(pos.x, pos.y + 0.1f, pos.z);
            Quaternion rot = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            LSystems lSystemObj = Instantiate(lSystemPrefab, pos , rot);
            generatedLSystems.Add(lSystemObj);
        }
    }

    //-------------------------------------====[[[[[[ CATMULL-ROM SEGMENT AND POS CALCULATION HERE]]]]]]====-------------------------------------
    public Vector3 EvalutePosition(float t) 
    {
        t = Mathf.Clamp01(t);
        int pointIndex = Mathf.RoundToInt(t * (generatedPoints.Length - 1));
        return generatedPoints[pointIndex];
    }

    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        // Calculate the Catmull-Rom spline point at t value
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * ((2f * p1) +
                       (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    //-------------------------------------====[[[[[[ GIZMOS HERE ]]]]]]====-------------------------------------
    private void OnDrawGizmos()
    {
        BuildSplineFromHandles();
        Vector3[] points = generatedPoints;

        //DRAW LINE BETWEEN POINTS
        Gizmos.color = Color.white;
        int totalCatmullSegments = points.Length - 3;
        Vector3 previousPos = CatmullRom(points[0], points[1], points[2], points[3], 0f);

        for (int i = 0; i < totalCatmullSegments; i++)
        {
            Vector3 p0 = points[i];
            Vector3 p1 = points[i + 1];
            Vector3 p2 = points[i + 2];
            Vector3 p3 = points[i + 3];

            for (int s = 1; s <= segmentResolution; s++)
            {
                float t = s / (float)segmentResolution;
                Vector3 pos = CatmullRom(p0, p1, p2, p3, t);

                Gizmos.DrawLine(previousPos, pos);
                previousPos = pos;
            }
        }

        //DRAW SPHERES AT EACH POINT
        Gizmos.color = Color.yellow;
        if (enableGizmoPoints)
        {
            for (int i = 0; i < points.Length; i++)
            {
                Gizmos.DrawSphere(points[i], gizmoSphereRadius);
            }

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(points[0], gizmoSphereRadius * 1.5f); //p0 start point
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(points[1], gizmoSphereRadius * 1.5f); //p1 starting spline point
            int lastP = points.Length - 2;
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(points[lastP], gizmoSphereRadius * 1.5f); //pN - 2 ending spline point
            int endP = points.Length - 1;
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(points[endP], gizmoSphereRadius * 1.5f); //pN - 1 end point
        }

        //DRAW POINTS XYZ AXES
        if (enableGizmoAxes) 
        {
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 currentPoint = points[i];
                Vector3 xDir = new Vector3(currentPoint.x + gizmoAxesLength, currentPoint.y, currentPoint.z);
                Vector3 yDir = new Vector3(currentPoint.x, currentPoint.y + gizmoAxesLength, currentPoint.z);
                Vector3 zDir = new Vector3(currentPoint.x, currentPoint.y, currentPoint.z + gizmoAxesLength);

                Gizmos.color = Color.red;
                Gizmos.DrawLine(currentPoint, xDir);

                Gizmos.color = Color.green;
                Gizmos.DrawLine(currentPoint, yDir);

                Gizmos.color = Color.blue;
                Gizmos.DrawLine(currentPoint, zDir);
            }
        }
    }
}
