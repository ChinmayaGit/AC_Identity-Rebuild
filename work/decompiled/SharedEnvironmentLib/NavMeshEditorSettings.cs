using UnityEngine;

public class NavMeshEditorSettings : MonoBehaviour
{
	public string DoNotRemove = "NavMeshEditorSettings";

	public static int Revision = 42;

	public int SavedRevision;

	public float EdgeDetector_EdgeSearchRadius;

	public float EdgeDetector_MinEdgeLength;

	public float ClimbRouteHelper_MinHeightAboveStart;

	public float ClimbRouteHelper_MaxHeightAboveEnd;

	public float ClimbRouteHelper_MaxForwardDistance;

	public float ClimbRouteHelper_MaxSideDistance;

	public float ClimbRouteHelper_MinOverhang;

	public float ClimbRouteHelper_MaxOverhang;

	public float ClimbRouteHelper_MaxEdgeYaw;

	public float ClimbRouteHelper_MaxEdgeRoll;

	public float ClimbRouteHelper_MaxSideReach;

	public float ClimbRouteHelper_MaxGroundReach;

	public float ClimbRouteHelper_MaxWallReach;

	public float ClimbRouteHelper_BestWallReach;

	public float ClimbRouteHelper_MinWallReach;

	public float ClimbRouteHelper_HandSpacing;

	public float ClimbRouteHelper_MinClearRadius;

	public float ClimbRouteHelper_MaxEndHeight;

	public float ClimbRouteHelper_WeightDistanceFromEdge;

	public float ClimbRouteHelper_WeightGroundReach;

	public float ClimbRouteHelper_WeightWallReach;

	public float ClimbRouteHelper_WeightSideReach;

	public float ClimbRouteHelper_WeightHandSpacing;

	public float ClimbRouteHelper_WeightEndHeight;

	public float ClimbRouteHelper_WeightEndAngle;

	public float ClimbRouteHelper_WeightClearRadius;

	public Vector3 ClimbRouteHelper_LastEdgeOffset;

	public Vector3 ClimbRouteHelper_IntermediateEdgeOffset;

	public float OffMeshLinkGenerator_SampleDistance;

	public float OffMeshLinkGenerator_NavMeshSnapRadius;

	public float OffMeshLinkGenerator_MinSeparation;

	public float OffMeshLinkGenerator_MinPathStretch;

	public float OffMeshLinkGenerator_DistanceFromEdge;

	public bool OffMeshLinkGenerator_AutoAttachEdges;

	public int OffMeshLinkGenerator_GenerateIterations;

	public float OffMeshLinkGenerator_MinLinkQuality;

	public bool OffMeshLinkGenerator_OptimizeEnabled;

	public int OffMeshLinkGenerator_OptimizeIterations;

	public float OffMeshLinkGenerator_OptimizeSearchRadius;

	public bool OffMeshLinkGenerator_ClimbEnabled;

	public bool OffMeshLinkGenerator_RunPassOverEnabled;

	public float OffMeshLinkGenerator_RunPassOverMinObstacleHeight;

	public float OffMeshLinkGenerator_RunPassOverMaxObstacleHeight;

	public float OffMeshLinkGenerator_RunPassOverMaxFallHeight;

	public float OffMeshLinkGenerator_RunPassOverMaxHeightDifference;

	public float OffMeshLinkGenerator_RunPassOverMaxFacingAngle;

	public float OffMeshLinkGenerator_RunPassOverMaxSideOffset;

	public bool OffMeshLinkGenerator_JumpDownEnabled;

	public float OffMeshLinkGenerator_JumpMaxObstacleHeight;

	public float OffMeshLinkGenerator_JumpDownMinFallAngle;

	public float OffMeshLinkGenerator_JumpDownMinHeight;

	public float OffMeshLinkGenerator_JumpDownMaxHeight;

	public float NavMeshEditor_StatsAutoRefreshPeriod;

	public bool NavMeshEditor_StatsAutoRefresh;

	public int NavMeshEditor_MaxEditableLinks;

	public Transform NavMeshEditor_LinkParent;

	public int GizmoHelper_MaxDetailDisplay;

	public float GizmoHelper_BadLinkThreshold;

	public float GizmoHelper_GoodLinkThreshold;

	public float GizmoHelper_DisplayDistance;
}
