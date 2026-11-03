using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AcierProtocol;
using Assets.Scripts.UI.Utils;
using SharedBaseLib.Serialization;
using UnityEngine;

namespace Assets.Scripts.Tracking;

public static class TrackingManager
{
	private sealed class TrackPointHandle
	{
		public DateTime CreationTime { get; private set; }

		public TrackPoint DataPoint { get; private set; }

		public Id<TrackPoint> Id { get; private set; }

		public TrackPointHandle(TrackPoint _data, Id<TrackPoint> _id)
		{
			Id = _id;
			CreationTime = DateTime.UtcNow;
			DataPoint = _data;
		}

		public TrackPointHandle(DataSerializerBase _ser)
		{
			Serialize(_ser);
		}

		public void Serialize(DataSerializerBase _ser)
		{
			CreationTime = new DateTime(_ser.SerializeInt64("CreationTime", CreationTime.Ticks));
			Id.Serialize(_ser);
			if (_ser.Mode == EMode.EMode_Read)
			{
				Type type = _ser.SerializeType("Type");
				DataPoint = (TrackPoint)Activator.CreateInstance(type);
			}
			else
			{
				_ser.SerializeType("Type", DataPoint.GetType());
			}
			DataPoint.Serialize(_ser);
		}
	}

	private const long k_sizeLimit = 1048576L;

	private static Dictionary<long, TrackPointHandle> s_trackPoints;

	private static long s_fileSize;

	private static IdGenerator<TrackPoint> s_generator;

	private static readonly object s_lock;

	private static bool s_loading;

	private static bool s_saving;

	private static readonly string DataDirectory;

	private static readonly string DataPath;

	public static bool TrackEnabled => s_fileSize < 1048576 && Initialized;

	public static bool Initialized { get; private set; }

	static TrackingManager()
	{
		s_trackPoints = new Dictionary<long, TrackPointHandle>(256);
		s_fileSize = 0L;
		s_generator = new IdGenerator<TrackPoint>();
		s_lock = new object();
		s_loading = false;
		s_saving = false;
		DataDirectory = $"{Main.LoadSavedDataPath()}/track";
		DataPath = $"{DataDirectory}/tracking.bin";
		Initialized = false;
	}

	public static Id<TrackPoint> AddCDNTrackPoint(DateTime _start, DateTime _end, string _url, long _fileSize, bool _save = true)
	{
		return AddTrackPoint(new CDNDownloadTrackPoint(_url, _start, _end, _fileSize), _save);
	}

	public static Id<TrackPoint> AddCDNTrackPoint(DateTime _start, DateTime _end, DateTime _install, string _url, long _fileSize, bool _save = true)
	{
		return AddTrackPoint(new CDNDownloadTrackPoint(_url, _start, _end, _fileSize, _install), _save);
	}

	public static Id<TrackPoint> AddPushNotificationTrackPoint(bool _isServerTriggered, bool _save = true)
	{
		return AddTrackPoint(new PushNotificationTrackPoint(_isServerTriggered), _save);
	}

	public static Id<TrackPoint> AddAnimusCloseTrackPoint(bool _save = true)
	{
		return Id<TrackPoint>.Invalid;
	}

	public static Id<TrackPoint> AddAnimusOpenTrackPoint(bool _save = true)
	{
		return Id<TrackPoint>.Invalid;
	}

	public static Id<TrackPoint> OpenScreenTrack(string _name, bool _save = true)
	{
		return AddTrackPoint(new ScreenTrackPoint(_name));
	}

	public static Id<TrackPoint> CloseScreenTrack(Id<TrackPoint> _id, string _name, bool _save = true)
	{
		lock (s_lock)
		{
			if (s_trackPoints.TryGetValue(_id, out var value) && value.DataPoint is ScreenTrackPoint screenTrackPoint && screenTrackPoint.Name == _name)
			{
				screenTrackPoint.CloseScreen();
			}
		}
		if (_save)
		{
			Persist();
		}
		return _id;
	}

	public static Id<TrackPoint> AddNotEnoughHCPopUpTrackPoint(bool _save = true)
	{
		return Id<TrackPoint>.Invalid;
	}

	public static Id<TrackPoint> AddSoundsTrackPoint(bool _isGlobalSoundOn, bool _isUISoundOn, bool _isMusicOn, bool _isThemeOn, bool _save = true)
	{
		return AddTrackPoint(new SoundsTrackPoint(_isGlobalSoundOn, _isUISoundOn, _isMusicOn, _isThemeOn), _save);
	}

	public static Id<TrackPoint> AddGamePadTrackPoint(string _usedIn, bool _save = true)
	{
		return AddTrackPoint(new GamePadTrackPoint(_usedIn), _save);
	}

	public static Id<TrackPoint> InviteFriendTrackPoint(string _messageType, bool _save = true)
	{
		return AddTrackPoint(new InviteFriendTrackPoint(_messageType), _save);
	}

	public static Id<TrackPoint> OpenEventProgressionScreen(string _triggeredFrom, bool _save = true)
	{
		return AddTrackPoint(new EventProgressionChallengeTrackPoint(_triggeredFrom), _save);
	}

	public static Id<TrackPoint> OpenShopCategoryTrackPoint(EShopCategory _category, bool _save = true)
	{
		return AddTrackPoint(new ShopCategoryTrackPoint(_category), _save);
	}

	public static Id<TrackPoint> OpenShopBundles(string _triggeredFrom, string _id, bool _save = true)
	{
		return AddTrackPoint(new ShopBundlesTrackPoint(_triggeredFrom, _id), _save);
	}

	public static Id<TrackPoint> CloseShopCategoryTrackPoint(Id<TrackPoint> _id, EShopCategory _category, bool _save = true)
	{
		lock (s_lock)
		{
			if (s_trackPoints.TryGetValue(_id, out var value) && value.DataPoint is ShopCategoryTrackPoint shopCategoryTrackPoint && shopCategoryTrackPoint.Category == _category)
			{
				shopCategoryTrackPoint.CloseCategory();
			}
		}
		if (_save)
		{
			Persist();
		}
		return _id;
	}

	public static Id<TrackPoint> AddAssetBundleTrackPoint(DateTime _start, DateTime _end, string _name, bool _success, bool _save = true)
	{
		return AddTrackPoint(new AssetBundleLoadTrackPoint(_start, _end, _name, _success), _save);
	}

	public static Id<TrackPoint> AddAppLaunchTrackPoint(bool _save = true)
	{
		return AddTrackPoint(new AppLaunchTrackPoint(), _save);
	}

	public static Id<TrackPoint> AddSessionTrackPoint(DateTime _start, DateTime _end, bool _save = true)
	{
		return AddTrackPoint(new SessionTrackPoint(_start, _end), _save);
	}

	public static Id<TrackPoint> AddCallToActionTrackPoint(string _name, bool _accept, bool _closed, bool _save = true)
	{
		return AddTrackPoint(new CallToActionTrackPoint(_name, _accept, _closed), _save);
	}

	public static Id<TrackPoint> AddRevivePopupTrackPoint()
	{
		return AddTrackPoint(new RevivePopupTrackPoint());
	}

	public static ProtobufReport GetFullReport()
	{
		ProtoBufReporter protoBufReporter = new ProtoBufReporter();
		lock (s_lock)
		{
			foreach (KeyValuePair<long, TrackPointHandle> s_trackPoint in s_trackPoints)
			{
				s_trackPoint.Value.DataPoint.Report(protoBufReporter, s_trackPoint.Key);
			}
		}
		return protoBufReporter.MakeReport() as ProtobufReport;
	}

	public static void Save()
	{
		Persist();
	}

	public static void InitAsync()
	{
		Main.Instance.StartCoroutine(co_initialize());
	}

	public static void Clear()
	{
		lock (s_lock)
		{
			s_trackPoints.Clear();
		}
		Persist();
	}

	public static void Clear(TrackReport _report)
	{
		if (_report == null)
		{
			return;
		}
		lock (s_lock)
		{
			long[] trackIds = _report.TrackIds;
			for (int i = 0; i < trackIds.Length; i++)
			{
				s_trackPoints.Remove(trackIds[i]);
			}
		}
		Persist();
	}

	private static Id<TrackPoint> AddTrackPoint(TrackPoint _point, bool _save = true)
	{
		Id<TrackPoint> id = Id<TrackPoint>.Invalid;
		if (TrackEnabled)
		{
			lock (s_lock)
			{
				TrackPointHandle trackPointHandle = new TrackPointHandle(_point, s_generator.Next());
				id = trackPointHandle.Id;
				if (!s_trackPoints.ContainsKey(id))
				{
					s_trackPoints.Add(id, trackPointHandle);
				}
			}
			if (_save)
			{
				Persist();
			}
		}
		return id;
	}

	private static void Persist()
	{
		Main.Instance.StartCoroutine(co_saveAsync());
	}

	private static IEnumerator co_saveAsync()
	{
		if (s_saving)
		{
			yield break;
		}
		CreateDirectory(DataDirectory);
		bool done = false;
		WaitForEndOfFrame wait = new WaitForEndOfFrame();
		long size = 0L;
		Exception error = null;
		ThreadPool.QueueUserWorkItem((object _state) =>
		{
			lock (s_lock)
			{
				try
				{
					using (FileStream stream = new FileStream(DataPath, FileMode.Create))
					{
						using DataSerializerBase dataSerializerBase = new DataBinarySerializer(stream, EMode.EMode_Write);
						dataSerializerBase.SerializeBeginNode("root");
						s_generator.Serialize(dataSerializerBase);
						dataSerializerBase.SerializeBeginNode("LoadBuffer");
						dataSerializerBase.SerializeItemCount(s_trackPoints.Count);
						foreach (KeyValuePair<long, TrackPointHandle> s_trackPoint in s_trackPoints)
						{
							dataSerializerBase.SerializeBeginNode("Key");
							dataSerializerBase.SerializeInt64("ID", s_trackPoint.Key);
							dataSerializerBase.SerializeEndNode();
							dataSerializerBase.SerializeBeginNode("Value");
							s_trackPoint.Value.Serialize(dataSerializerBase);
							dataSerializerBase.SerializeEndNode();
						}
						dataSerializerBase.SerializeEndNode();
						dataSerializerBase.SerializeEndNode();
					}
					FileInfo fileInfo = new FileInfo(DataPath);
					size = fileInfo.Length;
				}
				catch (Exception ex)
				{
					error = ex;
				}
				done = true;
			}
		});
		while (!done)
		{
			yield return wait;
		}
		if (error == null)
		{
			s_fileSize = size;
		}
		s_saving = false;
	}

	private static IEnumerator co_initialize()
	{
		yield return Main.Instance.StartCoroutine(co_loadAsync());
		Initialized = true;
	}

	private static IEnumerator co_loadAsync()
	{
		if (s_loading)
		{
			yield break;
		}
		FileInfo fileInfo = new FileInfo(DataPath);
		if (!fileInfo.Exists)
		{
			yield break;
		}
		s_loading = true;
		WaitForEndOfFrame wait = new WaitForEndOfFrame();
		bool done = false;
		Exception error = null;
		Dictionary<long, TrackPointHandle> loadBuffer = new Dictionary<long, TrackPointHandle>(256);
		long size = 0L;
		long mem = Profiler.GetMonoUsedSize();
		ThreadPool.QueueUserWorkItem((object _state) =>
		{
			lock (s_lock)
			{
				try
				{
					using (Stream stream = new FileStream(DataPath, FileMode.Open, FileAccess.Read))
					{
						using DataSerializerBase dataSerializerBase = new DataBinarySerializer(stream, EMode.EMode_Read);
						dataSerializerBase.SerializeBeginNode("root");
						s_generator.Serialize(dataSerializerBase);
						dataSerializerBase.SerializeBeginNode("LoadBuffer");
						loadBuffer.Clear();
						int num = dataSerializerBase.SerializeItemCount();
						for (int i = 0; i < num; i++)
						{
							dataSerializerBase.SerializeBeginNode("Key");
							long key = dataSerializerBase.SerializeInt64("ID", 0L);
							dataSerializerBase.SerializeEndNode();
							dataSerializerBase.SerializeBeginNode("Value");
							TrackPointHandle value = new TrackPointHandle(dataSerializerBase);
							dataSerializerBase.SerializeEndNode();
							loadBuffer.Add(key, value);
						}
						dataSerializerBase.SerializeEndNode();
						dataSerializerBase.SerializeEndNode();
					}
					FileInfo fileInfo2 = new FileInfo(DataPath);
					size = fileInfo2.Length;
				}
				catch (Exception ex)
				{
					error = ex;
				}
				done = true;
			}
		});
		while (!done)
		{
			yield return wait;
		}
		mem = Profiler.GetMonoUsedSize() - mem;
		if (error == null)
		{
			s_fileSize = size;
			s_trackPoints = loadBuffer;
		}
		s_loading = false;
	}

	private static void CreateDirectory(string _path)
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(_path);
		if (!directoryInfo.Exists)
		{
			directoryInfo.Create();
		}
	}
}
