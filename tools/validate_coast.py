"""Python PORT of CoastOrbit (universal-variable Kepler propagation from a state vector, approach B).
Validated against the element-based propagator used since V1. The C# still has to be run in Unity
(Apollo > Run Simulation Checks) to count as tested."""
import math

MU = 3.986004418e14
RE = 6_371_000.0
SQRT_MU = math.sqrt(MU)


def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def norm(a): return math.sqrt(dot(a, a))
def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)


# --- reference: element-based propagation (as in KeplerOrbit.cs) ---
def rot(O, i, w, x, y):
    cO, sO, cw, sw, ci, si = math.cos(O), math.sin(O), math.cos(w), math.sin(w), math.cos(i), math.sin(i)
    return ((cO * cw - sO * sw * ci) * x + (-cO * sw - sO * cw * ci) * y,
            (sO * cw + cO * sw * ci) * x + (-sO * sw + cO * cw * ci) * y,
            (sw * si) * x + (cw * si) * y)


def solve_kepler(m, e):
    ecc = m if e < 0.8 else math.pi
    for _ in range(50):
        d = (ecc - e * math.sin(ecc) - m) / (1 - e * math.cos(ecc))
        ecc -= d
        if abs(d) < 1e-12:
            break
    return ecc


def kepler_propagate(el, t):
    a, e, inc, O, w, M0 = el
    n = math.sqrt(MU / a**3)
    m = math.remainder(M0 + n * t, 2 * math.pi)
    E = solve_kepler(m, e)
    cE, sE = math.cos(E), math.sin(E)
    r = a * (1 - e * cE)
    xp, yp = a * (cE - e), a * math.sqrt(1 - e * e) * sE
    vs = math.sqrt(MU * a) / r
    return rot(O, inc, w, xp, yp), rot(O, inc, w, -vs * sE, vs * math.sqrt(1 - e * e) * cE)


# --- approach B: universal variables ---
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


def coast(r0, v0, dt):
    """State after dt seconds of two-body motion from (r0, v0). Bound orbits only."""
    r0m = norm(r0)
    alpha = 2.0 / r0m - dot(v0, v0) / MU           # 1/a
    assert alpha > 0, "bound orbits only"
    period = 2 * math.pi / (SQRT_MU * alpha**1.5)
    dt = math.remainder(dt, period)                 # the motion repeats every period
    sigma0 = dot(r0, v0) / SQRT_MU
    chi = SQRT_MU * dt * alpha
    for _ in range(100):
        z = alpha * chi * chi
        c, s = stumpff_c(z), stumpff_s(z)
        r = chi * chi * c + sigma0 * chi * (1 - z * s) + r0m * (1 - z * c)
        f = sigma0 * chi * chi * c + (1 - alpha * r0m) * chi**3 * s + r0m * chi - SQRT_MU * dt
        step = f / r
        chi -= step
        if abs(step) < 1e-9:
            break
    z = alpha * chi * chi
    c, s = stumpff_c(z), stumpff_s(z)
    r = chi * chi * c + sigma0 * chi * (1 - z * s) + r0m * (1 - z * c)
    fc = 1 - chi * chi * c / r0m
    gc = dt - chi**3 * s / SQRT_MU
    pos = add(mul(r0, fc), mul(v0, gc))
    fd = SQRT_MU / (r * r0m) * chi * (z * s - 1)
    gd = 1 - chi * chi * c / r
    vel = add(mul(r0, fd), mul(v0, gd))
    return pos, vel


cases = {
    "circular equatorial":         (RE + 400e3, 0.0, 0.0, 0.0, 0.0, 0.0),
    "circular inclined 32.5":      (RE + 185e3, 0.0, math.radians(32.5), 0.7, 0.0, 0.4),
    "elliptical inclined 51.6":    (12.0e6, 0.3, math.radians(51.6), 0.7, 1.1, 0.4),
    "elliptical equatorial":       (9.0e6, 0.1, 0.0, 0.0, 2.0, 1.0),
    "elliptical retro equatorial": (9.0e6, 0.1, math.pi, 0.0, 2.0, 1.0),
    "near-circular e=1e-4":        (RE + 300e3, 1e-4, math.radians(28.5), 1.2, 0.5, 2.0),
    "highly elliptical e=0.7":     (24.0e6, 0.7, math.radians(63.4), 0.5, 4.5, 2.5),
    "polar":                       (RE + 600e3, 0.02, math.radians(90), 2.0, 1.0, 3.0),
}

worst_pos = worst_vel = 0.0
for name, el in cases.items():
    wp = wv = 0.0
    for t0 in (0.0, 1234.5, 5000.0, 40000.0):
        r0, v0 = kepler_propagate(el, t0)
        for dt in (0.0, 1.0, 777.0, 5000.0, 20000.0, 86164.0, 1.0e6, -500.0, -20000.0):
            ra, va = kepler_propagate(el, t0 + dt)
            rb, vb = coast(r0, v0, dt)
            wp = max(wp, norm(sub(ra, rb)))
            wv = max(wv, norm(sub(va, vb)))
    print(f"{name:30s} worst position error {wp:.2e} m, velocity {wv:.2e} m/s")
    worst_pos, worst_vel = max(worst_pos, wp), max(worst_vel, wv)
LIMIT_POS, LIMIT_VEL = 1e-3, 1e-6
assert worst_pos < LIMIT_POS and worst_vel < LIMIT_VEL, (worst_pos, worst_vel)
print(f"COAST MATCHES ELEMENT PROPAGATION (limits {LIMIT_POS} m, {LIMIT_VEL} m/s)")

# Conservation over many orbits, stepping the state forward in 1 s chunks like the simulation does.
r, v = kepler_propagate(cases["elliptical inclined 51.6"], 0.0)
E0 = dot(v, v) / 2 - MU / norm(r)
H0 = norm(cross(r, v))
worst_e = worst_h = 0.0
for _ in range(20000):
    r, v = coast(r, v, 1.0)
    worst_e = max(worst_e, abs(dot(v, v) / 2 - MU / norm(r) - E0) / abs(E0))
    worst_h = max(worst_h, abs(norm(cross(r, v)) - H0) / H0)
print(f"20000 x 1 s chained steps: energy drift {worst_e:.2e}, angular momentum drift {worst_h:.2e}")
assert worst_e < 1e-9 and worst_h < 1e-9

# Velocity changes are exact state edits. Burn tests from the default orbit.
base = (RE + 185e3, 0.0, math.radians(32.5), 0.0, 0.0, 0.0)
r, v = kepler_propagate(base, 1000.0)
vh = mul(v, 1 / norm(v))
print("\nImpulsive velocity changes from the default orbit:")


def elements(r, v):
    energy = dot(v, v) / 2 - MU / norm(r)
    a = -MU / (2 * energy)
    ev = mul(sub(mul(r, dot(v, v) - MU / norm(r)), mul(v, dot(r, v))), 1 / MU)
    e = norm(ev)
    return a, e, a * (1 - e) - RE, a * (1 + e) - RE


for dv in (100.0, 10.0, -10.0, -30.0, -60.0):
    v2 = add(v, mul(vh, dv))
    a, e, peri, apo = elements(r, v2)
    print(f"  dv {dv:+6.1f} m/s: periapsis {peri / 1000:8.2f} km, apoapsis {apo / 1000:8.2f} km")
    if dv == 100.0:
        assert abs(apo - 532_440) < 200 and abs(peri - 185_000) < 10
        # propagate the new state half a period and check apoapsis/periapsis radii are reached
        period = 2 * math.pi * math.sqrt(a**3 / MU)
        rmax = max(norm(coast(r, v2, period * k / 400)[0]) for k in range(401))
        assert abs(rmax - RE - apo) < 5.0, rmax - RE - apo
print("  +100 m/s gives apoapsis about 532 km; propagated trajectory reaches it")

# Default orbit: circular state has no singularity problems any more.
r, v = kepler_propagate(base, 0.0)
a, e, peri, apo = elements(r, v)
assert e < 1e-9
print(f"  default orbit from state vector: e = {e:.1e}, no special-casing needed")
print("ALL COAST CHECKS PASSED")
