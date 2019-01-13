using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Photon;

namespace SubnauticaPhoton
{
    public class NetPlayerSync : PunBehaviour, IPunObservable
    {
        public Animator animator;

        Vector3 lastPosition = Vector3.zero;
        Vector3 lastRotation = Vector3.zero;
        Vector3 lastVelocity = Vector3.zero;

        Vector3 animVelocity = Vector3.zero;

        float viewPitch = 0;
        float lerpSpeed = 0.1f;
        bool isUnderwater = false;

        void LateUpdate()
        {
            if (!photonView.isMine)
            {
                transform.position = Vector3.Lerp(transform.position, lastPosition, lerpSpeed);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(lastRotation), lerpSpeed);

                Debug.Log("Setting animator values for remote player object");
                animator.SetFloat("move_speed", animVelocity.magnitude);
                animator.SetFloat("move_speed_x", animVelocity.x);
                animator.SetFloat("move_speed_y", animVelocity.y);
                animator.SetFloat("move_speed_z", animVelocity.z);

                animator.SetFloat("view_pitch", viewPitch);
                animator.SetBool("isUnderwater", isUnderwater);
            }
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if(stream.isWriting)
            {
                stream.SendNext(transform.position);
                stream.SendNext(transform.eulerAngles);


                Vector3 animvel = new Vector3(animator.GetFloat("move_speed_x"), animator.GetFloat("move_speed_y"), animator.GetFloat("move_speed_z"));
                stream.SendNext(animvel);
                stream.SendNext(animator.GetFloat("view_pitch"));
                stream.SendNext(animator.GetBool("isUnderwater"));
            }
            else
            {
                lastPosition = (Vector3)stream.ReceiveNext();
                lastRotation = (Vector3)stream.ReceiveNext();

                animVelocity = (Vector3)stream.ReceiveNext();
                viewPitch = (float)stream.ReceiveNext();
                isUnderwater = (bool)stream.ReceiveNext();
                Debug.Log("Receiving animation data from: "+info.sender.NickName);
            }
        }
    }
}
