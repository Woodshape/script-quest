function on_tick(self)
    local target = self.get_nearest_enemy()
    if not target then return end

    local dist = self.distance_to(target)

    -- Mage keeps distance and attacks from range
    if dist <= 2.0 then
        -- Too close, back away
        self.move_away_from(target)
    elseif dist <= 5.0 then
        -- In range, attack
        self.attack(target)
    else
        -- Move closer
        self.move_towards(target)
    end
end
