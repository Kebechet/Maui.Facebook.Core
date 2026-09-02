using System;
using Foundation;
using ObjCRuntime;
using UIKit;

// A curated binding, written by hand against the headers shipped inside
// nativelib/FBSDKCoreKit.xcframework - not an Objective Sharpie dump. FBSDKCoreKit exposes 220
// headers; the wrapper needs three types. Binding only those keeps the surface reviewable, keeps
// this file buildable on Windows (Sharpie is macOS-only), and means a native bump is "swap the
// frameworks and rebuild" rather than "regenerate and re-curate". The source header is named on
// every type so a bump can be checked against it.
namespace FacebookCoreiOS
{
	// FBSDKAppEvents.h
	// @interface FBSDKAppEvents : NSObject
	[BaseType (typeof(NSObject), Name = "FBSDKAppEvents")]
	[DisableDefaultCtor]
	interface FBSDKAppEvents
	{
		// @property (class, nonatomic, readonly, strong) FBSDKAppEvents *shared;
		[Static]
		[Export ("shared", ArgumentSemantic.Strong)]
		FBSDKAppEvents Shared { get; }

		// @property (nonatomic) FBSDKAppEventsFlushBehavior flushBehavior;
		[Export ("flushBehavior", ArgumentSemantic.Assign)]
		FBSDKAppEventsFlushBehavior FlushBehavior { get; set; }

		// @property (nullable, nonatomic, copy) NSString *userID;
		[NullAllowed, Export ("userID")]
		string UserID { get; set; }

		// @property (nonatomic, readonly) NSString *anonymousID;
		[Export ("anonymousID")]
		string AnonymousID { get; }

		// -(void)logEvent:(FBSDKAppEventName)eventName;
		// FBSDKAppEventName is `typedef NSString *` (NS_TYPED_EXTENSIBLE_ENUM), so a plain string binds it.
		[Export ("logEvent:")]
		void LogEvent (string eventName);

		// -(void)logEvent:(FBSDKAppEventName)eventName valueToSum:(double)valueToSum;
		[Export ("logEvent:valueToSum:")]
		void LogEvent (string eventName, double valueToSum);

		// -(void)logEvent:(FBSDKAppEventName)eventName parameters:(nullable NSDictionary<FBSDKAppEventParameterName, id> *)parameters;
		[Export ("logEvent:parameters:")]
		void LogEvent (string eventName, [NullAllowed] NSDictionary<NSString, NSObject> parameters);

		// -(void)logEvent:(FBSDKAppEventName)eventName valueToSum:(double)valueToSum parameters:(nullable NSDictionary<FBSDKAppEventParameterName, id> *)parameters;
		[Export ("logEvent:valueToSum:parameters:")]
		void LogEvent (string eventName, double valueToSum, [NullAllowed] NSDictionary<NSString, NSObject> parameters);

		// -(void)logPurchase:(double)purchaseAmount currency:(NSString *)currency;
		[Export ("logPurchase:currency:")]
		void LogPurchase (double purchaseAmount, string currency);

		// -(void)logPurchase:(double)purchaseAmount currency:(NSString *)currency parameters:(nullable NSDictionary<FBSDKAppEventParameterName, id> *)parameters;
		[Export ("logPurchase:currency:parameters:")]
		void LogPurchase (double purchaseAmount, string currency, [NullAllowed] NSDictionary<NSString, NSObject> parameters);

		// -(void)activateApp;
		[Export ("activateApp")]
		void ActivateApp ();

		// -(void)flush;
		[Export ("flush")]
		void Flush ();

		// -(void)clearUserData;
		[Export ("clearUserData")]
		void ClearUserData ();
	}

	// FBSDKCoreKit-Swift.h
	// SWIFT_CLASS_NAMED("Settings") @interface FBSDKSettings : NSObject
	// A Swift class published to Objective-C as @objc(FBSDKSettings), so the runtime name is the
	// plain one and needs no _TtC mangling.
	[BaseType (typeof(NSObject), Name = "FBSDKSettings")]
	interface FBSDKSettings
	{
		// @property (nonatomic, class, readonly, strong) FBSDKSettings * _Nonnull sharedSettings;
		[Static]
		[Export ("sharedSettings", ArgumentSemantic.Strong)]
		FBSDKSettings SharedSettings { get; }

		// @property (nonatomic, readonly, copy) NSString * _Nonnull sdkVersion;
		[Export ("sdkVersion")]
		string SdkVersion { get; }

		// @property (nonatomic) BOOL isAutoLogAppEventsEnabled;
		[Export ("isAutoLogAppEventsEnabled")]
		bool IsAutoLogAppEventsEnabled { get; set; }

		// @property (nonatomic) BOOL isAdvertiserIDCollectionEnabled;
		[Export ("isAdvertiserIDCollectionEnabled")]
		bool IsAdvertiserIDCollectionEnabled { get; set; }

		// @property (nonatomic) BOOL isAdvertiserTrackingEnabled;
		[Export ("isAdvertiserTrackingEnabled")]
		bool IsAdvertiserTrackingEnabled { get; set; }

		// @property (nonatomic) BOOL isSKAdNetworkReportEnabled;
		[Export ("isSKAdNetworkReportEnabled")]
		bool IsSKAdNetworkReportEnabled { get; set; }

		// @property (nonatomic) FBSDKAdvertisingTrackingStatus advertisingTrackingStatus;
		[Export ("advertisingTrackingStatus", ArgumentSemantic.Assign)]
		FBSDKAdvertisingTrackingStatus AdvertisingTrackingStatus { get; set; }

		// @property (nonatomic, copy) NSString * _Nullable appID;
		[NullAllowed, Export ("appID")]
		string AppID { get; set; }

		// @property (nonatomic, copy) NSString * _Nullable clientToken;
		[NullAllowed, Export ("clientToken")]
		string ClientToken { get; set; }

		// @property (nonatomic, copy) NSString * _Nullable displayName;
		[NullAllowed, Export ("displayName")]
		string DisplayName { get; set; }
	}

	// FBSDKCoreKit-Swift.h
	// SWIFT_CLASS_NAMED("ApplicationDelegate") @interface FBSDKApplicationDelegate : NSObject
	[BaseType (typeof(NSObject), Name = "FBSDKApplicationDelegate")]
	[DisableDefaultCtor]
	interface FBSDKApplicationDelegate
	{
		// @property (nonatomic, class, readonly, strong) FBSDKApplicationDelegate * _Nonnull sharedInstance;
		[Static]
		[Export ("sharedInstance", ArgumentSemantic.Strong)]
		FBSDKApplicationDelegate SharedInstance { get; }

		// -(void)initializeSDK;
		[Export ("initializeSDK")]
		void InitializeSDK ();

		// -(BOOL)application:(UIApplication * _Nonnull)application didFinishLaunchingWithOptions:(NSDictionary<UIApplicationLaunchOptionsKey, id> * _Nullable)launchOptions;
		[Export ("application:didFinishLaunchingWithOptions:")]
		bool FinishedLaunching (UIApplication application, [NullAllowed] NSDictionary launchOptions);

		// -(BOOL)application:(UIApplication * _Nonnull)application openURL:(NSURL * _Nonnull)url options:(NSDictionary<UIApplicationOpenURLOptionsKey, id> * _Nonnull)options;
		[Export ("application:openURL:options:")]
		bool OpenUrl (UIApplication application, NSUrl url, NSDictionary options);
	}
}
