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

        Vector3 lastnetworkPosition = Vector3.zero;
        Vector3 lastRotation = Vector3.zero;

        Vector3 previousPosition;

        float viewPitch = 0;
        float lerpSpeed = 0.1f;
        bool isUnderwater = false;

        void FixedUpdate()
        {
            if (!photonView.isMine)
            {
                transform.position = Vector3.Lerp(transform.position, lastnetworkPosition, lerpSpeed);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(lastRotation), lerpSpeed);

                Vector3 animVelocity = (transform.position - previousPosition);

                animator.SetFloat("move_speed", animVelocity.magnitude);
                animator.SetFloat("move_speed_x", animVelocity.x);
                animator.SetFloat("move_speed_y", animVelocity.y);
                animator.SetFloat("move_speed_z", animVelocity.z);

                animator.SetFloat("view_pitch", viewPitch);
                animator.SetBool("isUnderwater", isUnderwater);
                previousPosition = transform.position;
            }
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if(stream.isWriting)
            {
                stream.SendNext(transform.position);
                stream.SendNext(transform.eulerAngles);

                stream.SendNext(animator.GetFloat("view_pitch"));
                stream.SendNext(animator.GetBool("isUnderwater"));
            }
            else
            {
                lastnetworkPosition = (Vector3)stream.ReceiveNext();
                lastRotation = (Vector3)stream.ReceiveNext();

                viewPitch = (float)stream.ReceiveNext();
                isUnderwater = (bool)stream.ReceiveNext();
            }
        }
    }
}
