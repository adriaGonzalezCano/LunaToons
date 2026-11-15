"""Python PORT of the V2 powered-flight logic (RK8 burns + analytic coasting), using the exact coefficients shipped in
Assets/Scripts/OrbitalSim/Rk8Coefficients.cs. Validates the algorithm and picks the thresholds used by the C# checks.
The C# itself has to be run in Unity (Apollo > Run Simulation Checks) to count as tested."""
import math
import re

MU = 3.986004418e14
RE = 6_371_000.0
SQRT_MU = math.sqrt(MU)


def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def norm(a): return math.sqrt(dot(a, a))
def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)


# ---- coefficients, parsed from the C# file that ships ---------------------------------------------------------------------
src = open('/home/claude/apollo/Assets/Scripts/OrbitalSim/Rk8Coefficients.cs').read()


def parse_array(name):
    m = re.search(name + r"\s*=\s*\{\s*([^}]*)\}", src, re.S)
    return [float(x) for x in m.group(1).replace("\n", " ").split(",") if x.strip()]


C = parse_array("double\\[\\] C")
B = parse_array("double\\[\\] B")
A = [[float(x) for x in row.split(",") if x.strip()] for row in re.findall(r"new double\[\] \{([^}]*)\}", src)]
STAGES = len(B)
assert STAGES == 12 and len(A) == 12 and len(C) == 12 and abs(sum(B) - 1) < 1e-14
for i in range(STAGES):
    assert len(A[i]) == i and abs(sum(A[i]) - C[i]) < 1e-14, i      # row-sum condition


def rk8_step(f, t, y, h):
    k = []
    for i in range(STAGES):
        yi = list(y)
        for j in range(i):
            if A[i][j] != 0.0:
                aij = A[i][j] * h
                for n in range(len(y)):
                    yi[n] += aij * k[j][n]
        k.append(f(t + C[i] * h, yi))
    out = list(y)
    for i in range(STAGES):
        if B[i] != 0.0:
            bih = B[i] * h
            for n in range(len(y)):
                out[n] += bih * k[i][n]
    return out


# ---- two-body motion: reference element propagation and universal-variable coast ----------------------------------------
def rot(O, i, w, x, y):
    cO, sO, cw, sw, ci, si = math.cos(O), math.sin(O), math.cos(w), math.sin(w), math.cos(i), math.sin(i)
    return ((cO * cw - sO * sw * ci) * x + (-cO * sw - sO * cw * ci) * y,
            (sO * cw + cO * sw * ci) * x + (-sO * sw + cO * cw * ci) * y,
            (sw * si) * x + (cw * si) * y)


def kepler_state(el, t):
    a, e, inc, O, w, M0 = el
    n = math.sqrt(MU / a**3)
    m = math.remainder(M0 + n * t, 2 * math.pi)
    E = m if e < 0.8 else math.copysign(math.pi, m)
    for _ in range(100):
        d = (E - e * math.sin(E) - m) / (1 - e * math.cos(E))
        E -= d
        if abs(d) < 1e-14:
            break
    cE, sE = math.cos(E), math.sin(E)
    r = a * (1 - e * cE)
    vs = math.sqrt(MU * a) / r
    return rot(O, inc, w, a * (cE - e), a * math.sqrt(1 - e * e) * sE), rot(O, inc, w, -vs * sE, vs * math.sqrt(1 - e * e) * cE)


def stumpff_c(z):
    if abs(z) < 1e-6: return 0.5 - z / 24.0 + z * z / 720.0
    if z > 0: return (1 - math.cos(math.sqrt(z))) / z
    return (math.cosh(math.sqrt(-z)) - 1) / (-z)


def stumpff_s(z):
    if abs(z) < 1e-6: return 1.0 / 6.0 - z / 120.0 + z * z / 5040.0
    if z > 0:
        s = math.sqrt(z)
        return (s - math.sin(s)) / (s * s * s)
    s = math.sqrt(-z)
    return (math.sinh(s) - s) / (s * s * s)


class Coast:
    """CoastOrbit: exact two-body motion from a state vector. Bracketed Newton (the universal Kepler function is monotonic)."""

    def __init__(self, r0, v0, t0):
        self.r0, self.v0, self.t0 = r0, v0, t0
        self.r0m = norm(r0)
        self.alpha = 2 / self.r0m - dot(v0, v0) / MU
        assert self.alpha > 1e-12
        self.sigma0 = dot(r0, v0) / SQRT_MU
        self.period = 2 * math.pi / (SQRT_MU * self.alpha**1.5)
        self.a = 1 / self.alpha
        ev = mul(sub(mul(r0, dot(v0, v0) - MU / self.r0m), mul(v0, dot(r0, v0))), 1 / MU)
        self.e = norm(ev)
        self.iterations = 0

    @staticmethod
    def try_create(r, v, t):
        if not (norm(r) > 0): return None
        if not (2 / norm(r) - dot(v, v) / MU > 1e-12): return None
        return Coast(r, v, t)

    def periapsis_alt(self): return self.a * (1 - self.e) - RE
    def apoapsis_alt(self): return self.a * (1 + self.e) - RE

    def propagate(self, t):
        dt = math.remainder(t - self.t0, self.period)
        alpha, r0m, sigma0 = self.alpha, self.r0m, self.sigma0
        bound = 2 * math.pi / math.sqrt(alpha)
        lo, hi = -bound, bound
        chi = min(max(SQRT_MU * dt * alpha, lo), hi)
        for it in range(200):
            z = alpha * chi * chi
            c, s = stumpff_c(z), stumpff_s(z)
            r = chi * chi * c + sigma0 * chi * (1 - z * s) + r0m * (1 - z * c)
            f = sigma0 * chi * chi * c + (1 - alpha * r0m) * chi**3 * s + r0m * chi - SQRT_MU * dt
            if f < 0: lo = chi
            else: hi = chi
            nxt = chi - f / r
            if not (lo < nxt < hi): nxt = 0.5 * (lo + hi)
            done = abs(nxt - chi) < 1e-9
            chi = nxt
            if done: break
        self.iterations = it + 1
        z = alpha * chi * chi
        c, s = stumpff_c(z), stumpff_s(z)
        r = chi * chi * c + sigma0 * chi * (1 - z * s) + r0m * (1 - z * c)
        fc, gc = 1 - chi * chi * c / r0m, dt - chi**3 * s / SQRT_MU
        fd, gd = SQRT_MU / (r * r0m) * chi * (z * s - 1), 1 - chi * chi * c / r
        return add(mul(self.r0, fc), mul(self.v0, gc)), add(mul(self.r0, fd), mul(self.v0, gd))


# ---- test 1: coast solver robustness up to very eccentric orbits ------------------------------------------------------
print("1. Coast solver vs element propagation, including very eccentric orbits")
worst = 0.0
worst_iter = 0
for e in (0.0, 0.3, 0.7, 0.9, 0.97, 0.995):
    a = (RE + 185e3) / (1 - e)   # periapsis always 185 km altitude
    el = (a, e, math.radians(32.5), 0.7, 1.1, 0.4)
    assert a * (1 - e) > RE
    for t0 in (0.0, 1234.5, 40000.0):
        r0, v0 = kepler_state(el, t0)
        orbit = Coast(r0, v0, t0)
        period = orbit.period
        for k in range(-40, 41):
            dt = period * k / 37.0
            ra, va = kepler_state(el, t0 + dt)
            rb, vb = orbit.propagate(t0 + dt)
            err = norm(sub(ra, rb)) / max(1.0, norm(ra) / 7e6)  # position error, scaled for the size of the orbit
            worst = max(worst, err)
            worst_iter = max(worst_iter, orbit.iterations)
print(f"   worst scaled position error {worst:.2e} m, most iterations {worst_iter}")
assert worst < 1e-3 and worst_iter < 60

# ---- test 2: RK8 convergence order on gravity-only motion ------------------------------------------------------------------
print("2. RK8 convergence order (gravity only, e = 0.3, one period)")


def gravity(t, y):
    rr = norm(y[:3])
    k = -MU / rr**3
    return [y[3], y[4], y[5], k * y[0], k * y[1], k * y[2]]


el = (12e6, 0.3, math.radians(51.6), 0.7, 1.1, 0.4)
r0, v0 = kepler_state(el, 0.0)
period = 2 * math.pi * math.sqrt(el[0]**3 / MU)
errors = []
for steps in (40, 80, 160):
    y = list(r0) + list(v0)
    h = period / steps
    for i in range(steps):
        y = rk8_step(gravity, i * h, y, h)
    rf, vf = kepler_state(el, period)
    errors.append(norm(sub(tuple(y[:3]), rf)))
    print(f"   {steps:4d} steps/orbit: position error {errors[-1]:.3e} m")
ratio = errors[0] / errors[1]
print(f"   error ratio when halving the step: {ratio:.0f} (8th order gives about 256)")
assert 100 < ratio < 1000

y = list(r0) + list(v0)
steps = int(period)  # about 1 s per step
h = period / steps
for i in range(steps):
    y = rk8_step(gravity, i * h, y, h)
rf, vf = kepler_state(el, period)
print(f"   {steps} steps of about 1 s: position error {norm(sub(tuple(y[:3]), rf)):.2e} m, velocity {norm(sub(tuple(y[3:]), vf)):.2e} m/s")
assert norm(sub(tuple(y[:3]), rf)) < 1e-4


# ---- the powered simulation (port of SpacecraftSimulation) --------------------------------------------------------------------
POWERED_STEP = 0.05
MAX_CHUNK = 1.0
RESTART_ALT = 100e3


class Sim:
    def __init__(self, el, accel=3.0):
        r, v = kepler_state(el, 0.0)
        self.orbit = Coast.try_create(r, v, 0.0)
        self.t = 0.0
        self.pos, self.vel = r, v
        self.accel = accel
        self.thrust = False
        self.retro = False
        self.dv = 0.0
        self.cutoff = None
        self.crashed = False
        self.fixed = None      # inertial thrust direction (attitude hold); None = along the velocity

    def set_thrust(self, on, retro=False):
        self.thrust, self.retro = on, retro
        if not on: pass

    def _derivs(self, t, y):
        rr = norm(y[:3])
        k = -MU / rr**3
        sign = -1.0 if self.retro else 1.0
        if self.fixed is not None:
            d = [sign * self.accel * c for c in self.fixed]
            return [y[3], y[4], y[5], k * y[0] + d[0], k * y[1] + d[1], k * y[2] + d[2]]
        vm = norm(y[3:])
        a = self.accel * sign / vm
        return [y[3], y[4], y[5], k * y[0] + a * y[3], k * y[1] + a * y[4], k * y[2] + a * y[5]]

    def advance(self, dt):
        target = self.t + dt
        while self.t < target:
            chunk = min(self.t + (POWERED_STEP if self.thrust else MAX_CHUNK), target) - self.t
            if self.thrust:
                y = list(self.pos) + list(self.vel)
                y = rk8_step(self._derivs, self.t, y, chunk)
                npos, nvel = tuple(y[:3]), tuple(y[3:])
                new_orbit = Coast.try_create(npos, nvel, self.t + chunk)
                if new_orbit is None:
                    self.thrust = False
                    self.cutoff = "escape trajectory"
                    continue  # redo this chunk as a coast
                self.pos, self.vel, self.orbit = npos, nvel, new_orbit
                self.dv += self.accel * chunk
                self.t += chunk
            else:
                self.t += chunk
                self.pos, self.vel = self.orbit.propagate(self.t)
            if norm(self.pos) - RE < RESTART_ALT:
                self.crashed = True
                return


base = (RE + 185e3, 0.0, math.radians(32.5), 0.0, 0.0, 0.0)


def run_burn(frame_dt, burn_seconds, retro=False, accel=3.0):
    sim = Sim(base, accel)
    sim.set_thrust(True, retro)
    frames = int(round(burn_seconds / frame_dt))
    for _ in range(frames):
        sim.advance(frame_dt)
    sim.set_thrust(False)
    return sim


print("3. A 20 s prograde burn at 3 m/s^2 from the default orbit")
ref = run_burn(0.001, 20.0)          # reference: 1 ms steps
a60 = run_burn(1 / 60, 20.0)
a30 = run_burn(1 / 30, 20.0)
a144 = run_burn(1 / 144, 20.0)
print(f"   delta-v used {a60.dv:.6f} m/s (expected 60)")
for name, s in (("1/30 s frames", a30), ("1/60 s frames", a60), ("1/144 s frames", a144)):
    print(f"   {name}: position difference from the 1 ms reference {norm(sub(s.pos, ref.pos)):.2e} m, velocity {norm(sub(s.vel, ref.vel)):.2e} m/s")
    assert norm(sub(s.pos, ref.pos)) < 1e-3 and norm(sub(s.vel, ref.vel)) < 1e-6
assert abs(a60.dv - 60.0) < 1e-6

# impulsive prediction for 60 m/s at the burn midpoint region (compare apoapsis rise): same Delta-v as one impulse at t = 0
r0, v0 = kepler_state(base, 0.0)
imp = Coast.try_create(r0, add(v0, mul(v0, 60.0 / norm(v0))), 0.0)
fin = a60.orbit
print(f"   finite burn: periapsis {fin.periapsis_alt() / 1000:.2f} km, apoapsis {fin.apoapsis_alt() / 1000:.2f} km")
print(f"   impulsive 60 m/s: periapsis {imp.periapsis_alt() / 1000:.2f} km, apoapsis {imp.apoapsis_alt() / 1000:.2f} km")
rise_finite = fin.apoapsis_alt() - 185e3
rise_impulse = imp.apoapsis_alt() - 185e3
print(f"   apoapsis rise: finite {rise_finite / 1000:.2f} km vs impulsive {rise_impulse / 1000:.2f} km")
assert abs(rise_finite - rise_impulse) / rise_impulse < 0.02

print("4. Release: after the burn, coasting continues the integrated trajectory")
y = list(a60.pos) + list(a60.vel)
steps = int(a60.orbit.period)
h = a60.orbit.period / steps
for i in range(steps):
    y = rk8_step(gravity, 0, y, h)
rc, vc = a60.orbit.propagate(a60.t + a60.orbit.period)
print(f"   one period of analytic coast vs RK8 gravity-only: {norm(sub(tuple(y[:3]), rc)):.2e} m")
assert norm(sub(tuple(y[:3]), rc)) < 1e-3

print("5. Retrograde burn lowers the periapsis")
retro = run_burn(1 / 60, 10.0, retro=True)
print(f"   10 s retrograde: periapsis {retro.orbit.periapsis_alt() / 1000:.2f} km, delta-v {retro.dv:.1f} m/s")
assert retro.orbit.periapsis_alt() < 185e3 - 20e3 and abs(retro.orbit.apoapsis_alt() - 185e3) < 5e3

print("6. Holding the burn until the orbit would become unbound")
sim = Sim(base)
sim.set_thrust(True)
frames = 0
while sim.thrust and frames < 200000:
    sim.advance(0.05)
    frames += 1
print(f"   engine cut off after {sim.t:.0f} s ({sim.dv:.0f} m/s used), reason: {sim.cutoff}, orbit eccentricity {sim.orbit.e:.5f}, bound: {sim.orbit.alpha > 0}")
assert sim.cutoff == "escape trajectory" and sim.orbit.e < 1 and 900 < sim.t < 1300
rc, vc = sim.orbit.propagate(sim.t + 3600)
print(f"   coasting afterwards still propagates: altitude {norm(rc) / 1000 - RE / 1000:.0f} km after 1 h")

print("7. Attitude hold (V2 step 4): thrust along a FIXED inertial direction (the body +X axis does not follow the orbit)")
def hold_burn(seconds, retro, frame=1/60, start=None):
    sim = Sim(base)
    if start: 
        sim.pos, sim.vel = start
        sim.orbit = Coast.try_create(sim.pos, sim.vel, 0.0)
    sim.fixed = tuple(c / norm(sim.vel) for c in sim.vel)
    sim.set_thrust(True, retro)
    for _ in range(int(round(seconds / frame))):
        sim.advance(frame)
        if not sim.thrust: break
    return sim
h20 = hold_burn(20.0, False)
print(f"   20 s +X: periapsis {h20.orbit.periapsis_alt()/1000:.3f} km, apoapsis {h20.orbit.apoapsis_alt()/1000:.3f} km, delta-v {h20.dv:.4f}")
h10 = hold_burn(10.0, True)
print(f"   10 s -X: periapsis {h10.orbit.periapsis_alt()/1000:.3f} km, apoapsis {h10.orbit.apoapsis_alt()/1000:.3f} km")
r0, v0 = kepler_state(base, 0.0)
vb = add(v0, mul(v0, 3000.0 / norm(v0)))
hc = hold_burn(200.0, False, frame=0.05, start=(r0, vb))
print(f"   high-energy start (+3000 m/s): cutoff {hc.cutoff!r} at t = {hc.t:.2f} s, e = {hc.orbit.e:.5f}")
assert hc.cutoff == "escape trajectory"
print("ALL BURN CHECKS PASSED")
