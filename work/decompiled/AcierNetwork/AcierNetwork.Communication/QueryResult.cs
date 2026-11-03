namespace AcierNetwork.Communication;

public class QueryResult : RequestResult
{
	public QueryResult(Status _status = Status.InProgress, float _progress = 0f)
		: base(_status, _progress)
	{
	}
}
