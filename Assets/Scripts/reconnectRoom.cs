using System.Text;
using TMPro;
using UnityEngine;

public class reconnectRoom : MonoBehaviour
{
    public CanvasGroup reconnectRoomUI;
    public TextMeshProUGUI reconnectRoomName;
    public int reconnectRoomId;

    void Update() {
        var client = WS_Client.Instance;
        // Check if there's a pending reconnect room ID from WS_Client
        string _pendingReconnectRoomId = client != null ? client.pendingReconnectRoomId : null;
        if (client != null && !string.IsNullOrEmpty(_pendingReconnectRoomId))
        {
            //Debug.Log("Pending Reconnect Room ID: " + _pendingReconnectRoomId);
            reconnectRoomId = int.Parse(_pendingReconnectRoomId.Replace("room", ""));
            this.showReconnectRoomUI(_pendingReconnectRoomId);
        }
        else
        {
            SetUI.Set(this.reconnectRoomUI, false);
        }
    }

    public void showReconnectRoomUI(string roomId) {
        SetUI.Set(this.reconnectRoomUI, true);
        if(this.reconnectRoomName != null) {
            StringBuilder roomName =  new StringBuilder();
            roomName.Append("Re-Join ");
            roomName.Append(roomId);
            this.reconnectRoomName.text = roomName.ToString();
        }
    }

    public void ReconnectRoom()
    {
        WS_Client.Instance.JoinGameRoom(reconnectRoomId);
        SetUI.Set(this.reconnectRoomUI, false);
        reconnectRoomId = 0;
        MainMenu.Instance?.gameStart();
        WS_Client.Instance.pendingReconnectRoomId = "";
    }
}
