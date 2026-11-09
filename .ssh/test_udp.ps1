$udpClient = New-Object System.Net.Sockets.UdpClient
$ipAddress = [System.Net.IPAddress]::Parse('129.146.116.47')
$remoteEndPoint = New-Object System.Net.IPEndPoint($ipAddress, 10013)  # Replace with your target IP and port
$message = [System.Text.Encoding]::UTF8.GetBytes("Hello, UDP Server!")
$udpClient.Send($message, $message.Length, $remoteEndPoint)
$udpClient.Close()