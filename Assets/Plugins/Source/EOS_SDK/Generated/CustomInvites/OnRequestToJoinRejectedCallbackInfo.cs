// Copyright Epic Games, Inc. All Rights Reserved.
// This file is automatically generated. Changes to this file may be overwritten.

namespace Epic.OnlineServices.CustomInvites
{
	/// <summary>
	/// Output parameters for the <see cref="OnRequestToJoinRejectedCallback" /> Function.
	/// </summary>
	public struct OnRequestToJoinRejectedCallbackInfo : ICallbackInfo
	{
		/// <summary>
		/// Context that was passed into <see cref="CustomInvitesInterface.AddNotifyCustomInviteRejected" />
		/// </summary>
		public object ClientData { get; set; }

		/// <summary>
		/// User that sent the custom invite
		/// </summary>
		public ProductUserId TargetUserId { get; set; }

		/// <summary>
		/// Recipient Local user id
		/// </summary>
		public ProductUserId LocalUserId { get; set; }

		public Result? GetResultCode()
		{
			return null;
		}

		internal void Set(ref OnRequestToJoinRejectedCallbackInfoInternal other)
		{
			ClientData = other.ClientData;
			TargetUserId = other.TargetUserId;
			LocalUserId = other.LocalUserId;
		}
	}

	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 8)]
	internal struct OnRequestToJoinRejectedCallbackInfoInternal : ICallbackInfoInternal, IGettable<OnRequestToJoinRejectedCallbackInfo>, ISettable<OnRequestToJoinRejectedCallbackInfo>, System.IDisposable
	{
		private System.IntPtr m_ClientData;
		private System.IntPtr m_TargetUserId;
		private System.IntPtr m_LocalUserId;

		public object ClientData
		{
			get
			{
				object value;
				Helper.Get(m_ClientData, out value);
				return value;
			}

			set
			{
				Helper.Set(value, ref m_ClientData);
			}
		}

		public System.IntPtr ClientDataAddress
		{
			get
			{
				return m_ClientData;
			}
		}

		public ProductUserId TargetUserId
		{
			get
			{
				ProductUserId value;
				Helper.Get(m_TargetUserId, out value);
				return value;
			}

			set
			{
				Helper.Set(value, ref m_TargetUserId);
			}
		}

		public ProductUserId LocalUserId
		{
			get
			{
				ProductUserId value;
				Helper.Get(m_LocalUserId, out value);
				return value;
			}

			set
			{
				Helper.Set(value, ref m_LocalUserId);
			}
		}

		public void Set(ref OnRequestToJoinRejectedCallbackInfo other)
		{
			ClientData = other.ClientData;
			TargetUserId = other.TargetUserId;
			LocalUserId = other.LocalUserId;
		}

		public void Set(ref OnRequestToJoinRejectedCallbackInfo? other)
		{
			if (other.HasValue)
			{
				ClientData = other.Value.ClientData;
				TargetUserId = other.Value.TargetUserId;
				LocalUserId = other.Value.LocalUserId;
			}
		}

		public void Dispose()
		{
			Helper.Dispose(ref m_ClientData);
			Helper.Dispose(ref m_TargetUserId);
			Helper.Dispose(ref m_LocalUserId);
		}

		public void Get(out OnRequestToJoinRejectedCallbackInfo output)
		{
			output = new OnRequestToJoinRejectedCallbackInfo();
			output.Set(ref this);
		}
	}
}