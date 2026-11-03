# TODO: Escopeta de dados y comprar armas en la pared

Detalle de cada tarea (qué, campos, "Hecho cuando") en `tasks/plan.md`.
**Estado (2026-11-03):** plan cerrado con David. Siguiente paso: `/build` con **Unity cerrado**.
(La lista anterior, la del robot, está en git: commit 3e3d544.)

- [x] T1 Renombrar WeaponReloadAnimator.cs → WeaponAnimator.cs (con su .meta, mismo GUID)
- [x] T2 Compra: WeaponBase (IsAmmoFull, RefillAmmo), WeaponManager (CurrentWeaponPrefab), WallWeaponPickup (precio, munición, prompt) + WallWeaponPickupTests
- [x] T3 Prefabs: arreglar PF_Lanzacartas_Wall (GUID, 500), PF_EscopetaDados_Wall (Escopeta, 1500), valores de PF_EscopetaDados
- [ ] **Checkpoint:** compile + test + check_refs, commit y push (preguntar por el resto de cambios sin commitear)
- [ ] Pasos del Editor (David): Weapon Animator en la escopeta, MuzzlePoint, posición en mano, probar
