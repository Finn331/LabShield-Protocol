using UnityEngine;

namespace StarterAssets
{
    public class UICanvasControllerInput : MonoBehaviour
    {

        [Header("Output")]
        public StarterAssetsInputs starterAssetsInputs;

        private StarterAssetsInputs GetValidInputs()
        {
            if (starterAssetsInputs != null && starterAssetsInputs.gameObject.activeInHierarchy)
            {
                return starterAssetsInputs;
            }

            // Cari player yang aktif jika referensi awal mati/kosong
            StarterAssetsInputs[] allInputs = FindObjectsOfType<StarterAssetsInputs>();
            foreach (var input in allInputs)
            {
                if (input.gameObject.activeInHierarchy)
                {
                    starterAssetsInputs = input;
                    return input;
                }
            }
            return null;
        }

        public void VirtualMoveInput(Vector2 virtualMoveDirection)
        {
            var inputs = GetValidInputs();
            if (inputs != null) inputs.MoveInput(virtualMoveDirection);
        }

        public void VirtualLookInput(Vector2 virtualLookDirection)
        {
            var inputs = GetValidInputs();
            if (inputs != null) inputs.LookInput(virtualLookDirection);
        }

        public void VirtualJumpInput(bool virtualJumpState)
        {
            var inputs = GetValidInputs();
            if (inputs != null) inputs.JumpInput(virtualJumpState);
        }

        public void VirtualSprintInput(bool virtualSprintState)
        {
            var inputs = GetValidInputs();
            if (inputs != null) inputs.SprintInput(virtualSprintState);
        }
        
    }

}
