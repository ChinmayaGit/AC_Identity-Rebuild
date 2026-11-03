namespace BB.Mobile.Net;

public interface ICache
{
	void Expire();

	void Prefetch();
}
