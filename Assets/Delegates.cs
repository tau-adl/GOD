using System.Net;
using UnityEngine;

public delegate void CollisionEventHandler(MonoBehaviour source, Collision collision);
public delegate void TriggerEventHandler(MonoBehaviour source, Collider collider);

public delegate bool DatagramReceivedCallback(object sender, IPEndPoint remoteEndPoint, byte[] buffer, int offset, int count);
public delegate bool PartnerDiscoveredCallback(GodDiscovery sender, IPEndPoint remoteEndPoint, string additionalFields);