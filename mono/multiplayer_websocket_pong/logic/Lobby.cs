using Godot;
using System;
using Godot.Collections;

public class Lobby : Control
{
    private const int DefaultPort = 8910; // An arbitrary number.
    private const int MaxNumberOfPeers = 1; // How many people we want to have in a game

    private LineEdit _address;
    private LineEdit _port;
    private Button _hostButton;
    private Button _joinButton;
    private Label _statusOk;
    private Label _statusFail;
    private WebSocketServer _server;
    private WebSocketClient _client;
    String[] _protocols = {"my-protocol", "binary"};

    public override void _Ready()
    {
        // Get nodes - the generic is a class, argument is node path.
        _address = GetNode<LineEdit>("Address");
        _port = GetNode<LineEdit>("Port");
        _hostButton = GetNode<Button>("HostButton");
        _joinButton = GetNode<Button>("JoinButton");
        _statusOk = GetNode<Label>("StatusOk");
        _statusFail = GetNode<Label>("StatusFail");

        // Connect all callbacks related to networking.
        // Note: Use snake_case when talking to engine API.
        GetTree().Connect("network_peer_connected", this, nameof(PlayerConnected));
        GetTree().Connect("network_peer_disconnected", this, nameof(PlayerDisconnected));
        GetTree().Connect("connected_to_server", this, nameof(ConnectedOk));
        GetTree().Connect("connection_failed", this, nameof(ConnectedFail));
        GetTree().Connect("server_disconnected", this, nameof(ServerDisconnected));
    }

    // Network callbacks from SceneTree

    // Callback from SceneTree.
    private void PlayerConnected(int id)
    {
        // Someone connected, start the game!
        var pong = ResourceLoader.Load<PackedScene>("res://pong.tscn").Instance();

        // Connect deferred so we can safely erase it from the callback.
        pong.Connect("GameFinished", this, nameof(EndGame), new Godot.Collections.Array(), (int) ConnectFlags.Deferred);

        GetTree().Root.AddChild(pong);
        Hide();
    }

    private void PlayerDisconnected(int id)
    {
        EndGame(GetTree().IsNetworkServer() ? "Client disconnected" : "Server disconnected");
    }

    // Callback from SceneTree, only for clients (not server).
    private void ConnectedOk()
    {
        // This function is not needed for this project.
    }

    // Callback from SceneTree, only for clients (not server).
    private void ConnectedFail()
    {
        SetStatus("Couldn't connect", false);

        GetTree().NetworkPeer = null; // Remove peer.
        _hostButton.Disabled = false;
        _joinButton.Disabled = false;
    }

    private void ServerDisconnected()
    {
        EndGame("Server disconnected");
    }

    // Game creation functions

    private void EndGame(string withError = "")
    {
        if (HasNode("/root/Pong"))
        {
            // Erase immediately, otherwise network might show
            // errors (this is why we connected deferred above).
            GetNode("/root/Pong").Free();
            Show();
        }

        GetTree().NetworkPeer = null; // Remove peer.
        _hostButton.Disabled = false;
        _joinButton.Disabled = false;

        SetStatus(withError, false);
    }

    private void SetStatus(string text, bool isOk)
    {
        // Simple way to show status.
        if (isOk)
        {
            _statusOk.Text = text;
            _statusFail.Text = "";
        }
        else
        {
            _statusOk.Text = "";
            _statusFail.Text = text;
        }
    }

    private void OnHostPressed()
    {
        _server = new WebSocketServer();
        Error err = _server.Listen(Int32.Parse(_port.Text), _protocols, true);

        if (err != Error.Ok)
        {
            // Is another server running?
            SetStatus("Can't host, address in use.", false);
            return;
        }

        GetTree().NetworkPeer = _server;
        _hostButton.Disabled = true;
        _joinButton.Disabled = true;
        SetStatus("Waiting for player...", true);
    }

    private void OnJoinPressed()
    {
        string ip = _address.Text;
        string port = _port.Text;
        int iport = Int32.Parse(port);

        if (!ip.IsValidIPAddress())
        {
            SetStatus("IP address is invalid", false);
            return;
        }
        
        if (!port.IsValidInteger() && iport > 0 && iport < 65535)
        {
            SetStatus("Port is invalid", false);
            return;
        }

        _client = new WebSocketClient();
        string url = "ws://" + ip + ":" + port;
        _client.ConnectToUrl(url, _protocols, true);

        GetTree().NetworkPeer = _client;
        SetStatus("Connecting...", true);
    }

    //IMPORTANT NOTE
    //While there are many notices in the docs that Web Socket, unlike the MultiplayerAPI, needs you to manually poll,
    //This isn't necessarily true if you start the server & connect to it with gdMpApi = true
    //Below is still an example of how you could poll as either client or server
    /*
    public override void _Process(float delta){
        if(_server != null){
            if(_server.IsListening()){
                _peer.Poll();
            }
        }
        
        if(_client != null){
            if(_client.GetConnectionStatus() == NetworkedMultiplayerPeer.ConnectionStatus.Connecting || _client.GetConnectionStatus() == NetworkedMultiplayerPeer.ConnectionStatus.Connected){
                _client.Poll();
            }
        }
    }
    */
}
