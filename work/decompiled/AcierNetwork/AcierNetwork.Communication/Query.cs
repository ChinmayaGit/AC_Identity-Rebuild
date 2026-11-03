namespace AcierNetwork.Communication;

public class Query<T> : Request where T : Model
{
	public delegate void TQueryCallback(QueryResult _result, T _type);

	public TQueryCallback QueryCallback { get; private set; }

	public bool Ended { get; private set; }

	public Query(TQueryCallback _callback, Params _param)
		: base(_param)
	{
		QueryCallback = _callback;
		Ended = false;
	}

	public void EndRead(RequestResult _result, T _type)
	{
		QueryCallback((QueryResult)_result, _type);
		Ended = true;
	}

	public void Progress(float _progress)
	{
		QueryCallback(new QueryResult(RequestResult.Status.InProgress, _progress), null);
	}
}
