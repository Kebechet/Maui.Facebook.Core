using ObjCRuntime;

namespace FacebookCoreiOS
{
	// typedef NS_ENUM(NSUInteger, FBSDKAdvertisingTrackingStatus) - FBSDKAdvertisingTrackingStatus.h
	[Native]
	public enum FBSDKAdvertisingTrackingStatus : ulong
	{
		Allowed = 0,
		Disallowed = 1,
		Unspecified = 2
	}

	// typedef NS_ENUM(NSUInteger, FBSDKAppEventsFlushBehavior) - FBSDKAppEventsFlushBehavior.h
	[Native]
	public enum FBSDKAppEventsFlushBehavior : ulong
	{
		Auto = 0,
		ExplicitOnly = 1
	}
}
