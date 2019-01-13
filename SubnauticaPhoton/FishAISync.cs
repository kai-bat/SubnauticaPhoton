using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Photon;
using UnityEngine;

namespace SubnauticaPhoton
{
    public class FishAISync : PunBehaviour, IPunObservable
    {
        public Creature creature;

        void Update ()
        {
            creature.enabled = PhotonNetwork.isMasterClient;
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            Vector3 pos = transform.position;
            stream.Serialize(ref pos);
            transform.position = pos;

            Quaternion rot = transform.rotation;
            stream.Serialize(ref rot);
            transform.rotation = rot;
        }
    }
}
