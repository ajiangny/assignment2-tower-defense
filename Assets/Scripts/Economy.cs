using UnityEngine;

namespace TowerDefense
{
    public class Economy : MonoBehaviour
    {
        public static Economy Instance { get; private set; }

        [SerializeField, Min(0)] private int startingGold = 300;

        public int Gold { get; private set; }

        private void Awake()
        {
            Instance = this;
            Gold = startingGold;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Add(int amount) => Gold += amount;

        public bool TrySpend(int amount)
        {
            if (amount > Gold) return false;
            Gold -= amount;
            return true;
        }
    }
}
