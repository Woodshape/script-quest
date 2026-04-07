function on_tick(self)
    local target = self.get_nearest_enemy()
    if not target then return end

    local dist = self.distance_to(target)

    if dist <= 6.0 and self.can_use_ability("fireball") then
        self.use_ability_at("fireball", target.x, target.y)
    elseif dist <= 5.0 then
        self.attack(target)
    else
        self.move_towards(target)
    end
end
