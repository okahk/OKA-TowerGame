using TMPro;
using UnityEngine;

public enum Team
{
    FirstTeam = 0,
    SecondTeam = 1
}

public class QuestionTrigger : MonoBehaviour
{
    public int questionId;
    [SerializeField]
    private Team team;


    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the colliding object is a player
        if (other.CompareTag("MainPlayer") || other.CompareTag("Player"))
        {
            OnPlayerEnterQuestion(other.gameObject);
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Called every frame while the collider is inside the trigger
        if (other.CompareTag("MainPlayer") || other.CompareTag("Player"))
        {
            // You can add continuous collision logic here if needed
        }
    }

    private void OnPlayerEnterQuestion(GameObject player)
    {

        var client = WS_Client.Instance;
        var gameController = TowerGameController.Instance;
        CharacterController characterController = player.GetComponent<CharacterController>();
        if (characterController == null || client == null || gameController == null) return;

        int answerId = characterController.answerId;

        // Find corresponding PlayerData for the character that entered the trigger
        WS_Client.PlayerData targetPlayer = null;
        if (client.GameData?.players != null)
        {
            targetPlayer = client.GameData.players.Find(p => p.uid == characterController.UserId);
        }

        if (targetPlayer == null)
        {
            Debug.LogWarning($"QuestionTrigger: no PlayerData for uid={characterController.UserId}");
            return;
        }

        // Find the index of the scoreboard matching this player's key
        int playerIndex = -1;
        for (int i = 0; i < gameController.scoreboardControllers.Length; i++)
        {
            scoreboardController sb = gameController.scoreboardControllers[i].GetComponent<scoreboardController>();
            if (sb != null && sb.key == targetPlayer.player_id)
            {
                playerIndex = i;
                break;
            }
        }

        if (playerIndex != -1 && playerIndex % 2 == (int)this.team)
        {
            // Only submit from the owner. DO NOT clear GameData here �X wait for server broadcast.
            if (characterController.IsLocalPlayer)
            {
                Debug.Log($"QuestionTrigger: local submit uid={characterController.UserId} answerId={answerId}");
                gameController.RegisterLocalAnswerSubmission(answerId);
                _ = client.submitAnswer(answerId);

                // Hide local bubble immediately for visual feedback if desired
                characterController.showAnswerBubble(0, "");
            }
        }
    }
}

