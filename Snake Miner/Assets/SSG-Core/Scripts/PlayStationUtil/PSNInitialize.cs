using UnityEngine;
#if UNITY_PS5 || UNITY_PS4
using PSNSample;
using Unity.PSN.PS5.Users;
using UnityEngine.PS5;
using Unity.PSN.PS5.Aysnc;
using Unity.PSN.PS5.UDS;
using Unity.PSN.PS5;
#endif

namespace SSG_Core.Scripts.PlayStationUtil
{
	public class PSNInitialize : MonoBehaviour
	{
#if UNITY_PS5 || UNITY_PS4
	public void Start()
	{
		// yield return new WaitWhile(() => GameManager.Instance == null || GameManager.Instance.IsLoadingScreenShowing);

		// initialize the PSN main class
		try
		{
			var initResult = Unity.PSN.PS5.Main.Initialize();
			if (initResult.Initialized)
			{
				Debug.Log("PSN initialized");

				//Initalize users
				GamePad[] gamePads = GetComponents<GamePad>();
				User.Initialize(gamePads);

				// Create a request to start UDS
				UniversalDataSystem.StartSystemRequest request = new UniversalDataSystem.StartSystemRequest
				{
					PoolSize = 256 * 1024
				};

				// Create an asynchronous request to send the StartSystemRequest
				var requestOp = new AsyncRequest<UniversalDataSystem.StartSystemRequest>(request).ContinueWith((antecedent) =>
				{
					// This is where you put code that runs when the request completes
					// You can check if the request failed or succeeded here
					if (SonyNpMain.CheckAysncRequestOK(antecedent))
					{
						Debug.Log("PSN Setup successfully");
					}
					else
					{
						Debug.Log("PSN Initialization Failure");
						Debug.Log("result: " + JsonUtility.ToJson(antecedent.Request.Result));
						Debug.Log("api result: " + JsonUtility.ToJson(antecedent.Request.Result.apiResult));
						Debug.Log("message: " + JsonUtility.ToJson(antecedent.Request.Result.message));
					}
				});
				UniversalDataSystem.Schedule(requestOp);

				var userId = PS5Input.GetUsersDetails(0).userId;
				UserSystem.AddUserRequest userRequest = new UserSystem.AddUserRequest() { UserId = userId };

				var userRequestOp = new AsyncRequest<UserSystem.AddUserRequest>(userRequest).ContinueWith((antecedent) =>
				{
					if (SonyNpMain.CheckAysncRequestOK(antecedent))
					{
						Debug.Log("User System Setup successfully");
					}
					else
					{
						Debug.Log("User System Initialization Failure");
						Debug.Log("result: " + JsonUtility.ToJson(antecedent.Request.Result));
						Debug.Log("api result: " + JsonUtility.ToJson(antecedent.Request.Result.apiResult));
						Debug.Log("message: " + JsonUtility.ToJson(antecedent.Request.Result.message));
					}
				});

				UniversalDataSystem.Schedule(userRequestOp);
			}

		}
		catch (PSNException e)
		{
			Debug.LogError("Exception During Initialization : " + e.ExtendedMessage);
		}
	}
#endif

#if UNITY_PS5 || UNITY_PS4
	void Update()
	{
		try
		{
			Main.Update();
			User.CheckRegistration();
		}
		catch (Exception e)
		{
			Debug.LogError("Exception in Main Update: " + e.Message);
		}
	}
#endif
	}
}