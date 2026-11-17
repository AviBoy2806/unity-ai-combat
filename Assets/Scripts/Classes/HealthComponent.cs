using UnityEngine;
using System;
public class HealthComponent : MonoBehaviour {
 public event Action<float> onDamage, healthChanged;
 public float maxHealth=100; [SerializeField] float health=100;
 public float Health => health;
 internal void ResetEpisodeState(){health=maxHealth;}
 public void takeDamage(float damage) => GameCombat.CombatResolver.ApplyDamage(this,damage);
 internal float ApplyResolvedDamage(float damage) {
 if(!float.IsFinite(damage) || damage<=0 || health<=0)return 0;
 float actual=Mathf.Min(health,damage);health-=actual;healthChanged?.Invoke(health);onDamage?.Invoke(actual);return actual;
 }
}
