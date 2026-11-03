using SharedBaseLib.System;

public static class LinkTypeExtension
{
	public static int ToNavMeshLayer(this eLinkType _linkType)
	{
		return _linkType switch
		{
			eLinkType.JumpHorizontal => NavMeshLayers.JumpHorizontalLayer, 
			eLinkType.JumpDown => NavMeshLayers.JumpDownLayer, 
			eLinkType.JumpHorizontalToWall => NavMeshLayers.JumpHorizontalToWallLayer, 
			eLinkType.Climb => NavMeshLayers.ClimbLayer, 
			eLinkType.Balance => NavMeshLayers.BalanceLayer, 
			eLinkType.ClimbUpLadder => NavMeshLayers.ClimbUpLadderLayer, 
			eLinkType.ClimbDownLadder => NavMeshLayers.ClimbDownLadderLayer, 
			eLinkType.RunPassOver => NavMeshLayers.RunPassOverLayer, 
			eLinkType.RunPassOverDown => NavMeshLayers.RunPassOverDownLayer, 
			eLinkType.ParkourSequence => NavMeshLayers.ParkourSequenceLayer, 
			_ => NavMeshLayers.NotWalkableLayer, 
		};
	}
}
