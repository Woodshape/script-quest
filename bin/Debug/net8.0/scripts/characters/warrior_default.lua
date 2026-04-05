function on_tick(self)
    local hp_pct = self.get_hp() / self.get_max_hp()

    -- Find nearest enemy
    local target = self.get_nearest_enemy()
    if not target then return end

    local dist = self.distance_to(target)

    -- If in attack range, attack
    if dist <= 1.5 then
        self.attack(target)
    else
        -- Move towards the target
        self.move_towards(target)
    end
end
