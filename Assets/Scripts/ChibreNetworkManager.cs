using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class ChibreNetworkManager : NetworkManager
{
    [SerializeField]
    private Transform[] orderedSpawnLocations;

    private int playerCount = 0;

    public override void OnServerAddPlayer(NetworkConnection conn, short playerControllerId)
    {
        var spawnLocation = orderedSpawnLocations[playerCount];
        GameObject player = (GameObject)Instantiate(playerPrefab, spawnLocation.position, spawnLocation.rotation);
        NetworkServer.AddPlayerForConnection(conn, player, playerControllerId);
        playerCount++;
    }
}
