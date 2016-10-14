using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Tizen;

namespace Tizen.Messaging.Push
{
    internal class PushImpl
    {
        private static readonly object _lock = new object();
        private static PushImpl _instance;

        internal static PushImpl Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        Log.Info(Interop.PushClient.LogTag, "Creating New Instance");
                        _instance = new PushImpl();
                    }
                }
                return _instance;
            }
        }

        internal PushImpl()
        {
            //Empty
        }

        private IntPtr _connection;

        internal void PushServiceConnect(string pushAppId)
        {
            Interop.PushClient.VoidStateChangedCallback stateDelegate = (int state, string err, IntPtr userData) =>
            {
                if (err == null)
                {
                    err = "";
                }
                PushConnectionStateEventArgs args = new PushConnectionStateEventArgs((PushConnectionStateEventArgs.PushState)state, err);
                PushClient.StateChange(args);
            };
            Interop.Push.VoidNotifyCallback notifyDelegate = (IntPtr notification, IntPtr userData) =>
            {
                Interop.PushClient.ServiceError result;
                PushMessageEventArgs ob = new PushMessageEventArgs();
                string data;
                result = Interop.PushClient.GetNotificationData(notification, out data);
                if ((result == Interop.PushClient.ServiceError.None) && !(String.IsNullOrEmpty(data)))
                {
                    ob.AppData = data;
                }
                else
                {
                    ob.AppData = "";
                }
                string message;
                result = Interop.PushClient.GetNotificationMessage(notification, out message);
                if ((result == Interop.PushClient.ServiceError.None) && !(String.IsNullOrEmpty(message)))
                {
                    ob.Message = message;
                }
                else
                {
                    ob.Message = "";
                }
                string sender;
                result = Interop.PushClient.GetNotificationSender(notification, out sender);
                if ((result == Interop.PushClient.ServiceError.None) && !(String.IsNullOrEmpty(sender)))
                {
                    ob.Sender = sender;
                }
                else
                {
                    ob.Sender = "";
                }
                string sessioninfo;
                result = Interop.PushClient.GetNotificationSessionInfo(notification, out sessioninfo);
                if ((result == Interop.PushClient.ServiceError.None) && !(String.IsNullOrEmpty(sessioninfo)))
                {
                    ob.SessionInfo = sessioninfo;
                }
                else
                {
                    ob.SessionInfo = "";
                }
                string requestid;
                result = Interop.PushClient.GetNotificationRequestId(notification, out requestid);
                if ((result == Interop.PushClient.ServiceError.None) && !(String.IsNullOrEmpty(requestid)))
                {
                    ob.RequestId = requestid;
                }
                else
                {
                    ob.RequestId = "";
                }
                int time;
                result = Interop.PushClient.GetNotificationTime(notification, out time);
                DateTime utc;
                if ((result == Interop.PushClient.ServiceError.None) && (time != 0))
                {
                    Log.Info(Interop.PushClient.LogTag, "Ticks received: " + time);
                    utc = DateTime.SpecifyKind(new DateTime(1970, 1, 1).AddSeconds(time), DateTimeKind.Utc);
                    ob.ReceivedAt = utc.ToLocalTime();
                }
                else
                {
                    Log.Info(Interop.PushClient.LogTag, "No Date received");
                    ob.ReceivedAt = DateTime.Now;
                }
                int type = -1;
                result = Interop.PushClient.GetNotificationType(notification, out type);
                if (result == Interop.PushClient.ServiceError.None)
                {
                    ob.Type = type;
                }
                PushClient.Notify(ob);
                //Interop.Push.FreeNotification(notification);
                Log.Info(Interop.PushClient.LogTag, "Free Notification Done");
            };
            Interop.PushClient.ServiceError connectResult = Interop.Push.ServiceConnect(pushAppId, stateDelegate, notifyDelegate, IntPtr.Zero, out _connection);
            if (connectResult != Interop.PushClient.ServiceError.None)
            {
                Log.Error(Interop.PushClient.LogTag, "Connect failed with " + connectResult);
                throw PushExceptionFactory.CreateResponseException(connectResult);
            }
        }

        internal void PushServiceDisconnect()
        {
            Interop.PushClient.ServiceDisconnect(_connection);
            Log.Info(Interop.PushClient.LogTag, "PushServiceDisconnect Completed");
        }

        internal async Task<ServerResponse> PushServerRegister()
        {
            Log.Info(Interop.PushClient.LogTag, "Register Called");
            var task = new TaskCompletionSource<ServerResponse>();
            Interop.PushClient.VoidResultCallback registerResult = (Interop.PushClient.Result regResult, IntPtr msgPtr, IntPtr userData) =>
            {
                Log.Info(Interop.PushClient.LogTag, "Register Callback Called");
                string msg = "";
                if (msgPtr != IntPtr.Zero)
                {
                    msg = Marshal.PtrToStringAnsi(msgPtr);
                }
                ServerResponse response = new ServerResponse();
                response.ServerResult = (ServerResponse.Result)regResult;
                response.ServerMessage = msg;
                if (task.TrySetResult(response) == false)
                {
                    Log.Error(Interop.PushClient.LogTag, "Unable to set the Result for register");
                }
            };
            Interop.PushClient.ServiceError result = Interop.PushClient.ServiceRegister(_connection, registerResult, IntPtr.Zero);
            Log.Info(Interop.PushClient.LogTag, "Interop.Push.ServiceRegister Completed");
            if (result != Interop.PushClient.ServiceError.None)
            {
                Log.Error(Interop.PushClient.LogTag, "Register failed with " + result);
                task.SetException(PushExceptionFactory.CreateResponseException(result));
            }
            return await task.Task;
        }

        internal async Task<ServerResponse> PushServerUnregister()
        {
            var task = new TaskCompletionSource<ServerResponse>();
            Interop.PushClient.VoidResultCallback registerResult = (Interop.PushClient.Result regResult, IntPtr msgPtr, IntPtr userData) =>
            {
                Log.Info(Interop.PushClient.LogTag, "Unregister Callback Called");
                string msg = "";
                if (msgPtr != IntPtr.Zero)
                {
                    msg = Marshal.PtrToStringAnsi(msgPtr);
                }
                ServerResponse response = new ServerResponse();
                response.ServerResult = (ServerResponse.Result)regResult;
                response.ServerMessage = msg;
                if (task.TrySetResult(response) == false)
                {
                    Log.Error(Interop.PushClient.LogTag, "Unable to set the Result for Unregister");
                }
            };
            Interop.PushClient.ServiceError result = Interop.PushClient.ServiceDeregister(_connection, registerResult, IntPtr.Zero);
            if (result != Interop.PushClient.ServiceError.None)
            {
                task.SetException(PushExceptionFactory.CreateResponseException(result));
            }
            return await task.Task;
        }

        internal string GetRegistrationId()
        {
            string regID = "";
            Interop.PushClient.ServiceError result = Interop.PushClient.GetRegistrationId(_connection, out regID);
            if (result != Interop.PushClient.ServiceError.None)
            {
                throw PushExceptionFactory.CreateResponseException(result);
            }
            Log.Info(Interop.PushClient.LogTag, "Returning Reg Id: " + regID);
            return regID;
        }

        internal void GetUnreadNotifications()
        {
            Interop.PushClient.ServiceError result = Interop.PushClient.RequestUnreadNotification(_connection);
            if (result != Interop.PushClient.ServiceError.None)
            {
                throw PushExceptionFactory.CreateResponseException(result);
            }
        }

        internal static void Reset()
        {
            lock (_lock)
            {
                Log.Info(Interop.PushClient.LogTag, "Making _instance as null");
                _instance = null;
            }
        }
    }
}
