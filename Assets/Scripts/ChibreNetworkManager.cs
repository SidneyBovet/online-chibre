using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// A rather simple override of Unity's NetworkManager that makes sure players are seated correctly around the
/// table and lets them know about their id.
/// </summary>
public class ChibreNetworkManager : NetworkManager
{
    [SerializeField]
    private Transform[] orderedSpawnLocations;
    [SerializeField]
    private GameManager gameManager;

    private int playerCount = 0;

    public override void OnServerAddPlayer(NetworkConnection conn, short playerControllerId)
    {
        var spawnLocation = orderedSpawnLocations[playerCount];
        GameObject player = (GameObject)Instantiate(playerPrefab, spawnLocation.position, spawnLocation.rotation);
        NetworkServer.AddPlayerForConnection(conn, player, playerControllerId);
        gameManager.AddPlayer(player.GetComponent<Player>(), playerCount);
        playerCount++;
    }
}
