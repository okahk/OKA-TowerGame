using UnityEngine;
using TMPro;
using UnityEngine.UI;
using NativeWebSocket;

public class roomListController : MonoBehaviour
{
    public TextMeshProUGUI roomNoText;
    public TextMeshProUGUI playerNoText;
    public GameObject indicator, buttonIndicator;
    public Button joinButton;
    public Sprite[] jointButtonState;
    public int roomId;

    private WS_Client.RoomInfo room;

    void Start()
    {
        joinButton.onClick.AddListener(JoinRoom);
        roomNoText.text = "Room " + roomId.ToString();
        if (this.indicator == null) this.indicator.SetActive(true);
        if (this.buttonIndicator == null) this.buttonIndicator.SetActive(true);    
        roomListRefresh();
    }

    void Update()
    {
        roomListRefresh();
        if (this.indicator == null || this.buttonIndicator == null) return;
        if (WS_Client.Instance.websocket == null || WS_Client.Instance.websocket.State != WebSocketState.Open) {
            playerNoText.text = "<color=#FF000000>0</color>/6";
            joinButton.interactable = false;
            joinButton.GetComponent<Image>().sprite = jointButtonState[1];
            return;
        }
        else
        {
            if (WS_Client.Instance.RoomList != null)
            {
                //roomNoText.text = "Room " + roomId.ToString() + "---" + room.roomStatus;
                if (room == null)
                {
                    playerNoText.text = "<color=#FF000000>0</color>/6";
                    joinButton.interactable = false;
                    joinButton.GetComponent<Image>().sprite = jointButtonState[1];
                }
                else
                {
                    playerNoText.text = room.roomMembers.ToString() + "/6";
                    if (room.roomMembers >= 6 || room.roomStatus == "playing")
                    {
                        joinButton.interactable = false;
                    }
                    else
                    {
                        joinButton.interactable = true;
                    }
                    joinButton.GetComponent<Image>().sprite = jointButtonState[0];
                    this.indicator.SetActive(false);
                    this.buttonIndicator.SetActive(false);
                }
            }
        }
    }

    private void roomListRefresh()
    {
        if (WS_Client.Instance != null && WS_Client.Instance.RoomList != null)
        {
            room = WS_Client.Instance.RoomList.Find(r => r.roomId == "room" + roomId.ToString());
            if (room != null)
            {
                playerNoText.text = room.roomMembers.ToString() + "/6";
                return;
            }
        }
        playerNoText.text = "<color=#FF000000>0</color>/6";
    }

    public void JoinRoom()
    {
        if (room != null)
        {
            WS_Client.Instance.JoinGameRoom(roomId);
            MainMenu.Instance?.gameStart();
        }
    }
}
