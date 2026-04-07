function on_tick(self)
    local target = self.get_nearest_enemy()
    if not target then return end

    local dist = self.distance_to(target)

    if dist <= 1.5 and self.can_use_ability("shield_bash") then
        self.use_ability("shield_bash", target)
    elseif dist <= 1.5 then
        self.attack(target)
    else
        self.move_towards(target)
    end
end
