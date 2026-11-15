"""Python PORT of KeplerOrbit.TryFromState (M0 of V2, approach A: recompute elements from a state vector).
Validates the algorithm and thresholds; the C# still has to be run in Unity (Apollo > Run Simulation Checks)."""
import math

MU = 3.986004418e14
RE = 6_371_000.0


def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def norm(a): return math.sqrt(dot(a, a))
def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


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


def propagate(el, t):
    a, e, inc, O, w, M0 = el
    n = math.sqrt(MU / a**3)
    m = math.remainder(M0 + n * t, 2 * math.pi)
    E = solve_kepler(m, e)
    cE, sE = math.cos(E), math.sin(E)
    r = a * (1 - e * cE)
    xp, yp = a * (cE - e), a * math.sqrt(1 - e * e) * sE
    vs = math.sqrt(MU * a) / r
    vxp, vyp = -vs * sE, vs * math.sqrt(1 - e * e) * cE
    return rot(O, inc, w, xp, yp), rot(O, inc, w, vxp, vyp)


CIRCULAR_E = 1e-8
EQUATORIAL_SIN_I = 1e-9


def from_state(r, v, t0):
    rm = norm(r)
    v2 = dot(v, v)
    h = cross(r, v)
    hm = norm(h)
    energy = v2 / 2 - MU / rm
    if energy >= 0:
        return None
    a = -MU / (2 * energy)
    rv = dot(r, v)
    ev = tuple(((v2 - MU / rm) * r[k] - rv * v[k]) / MU for k in range(3))
    e = norm(ev)
    if e >= 1:
        return None
    hh = tuple(c / hm for c in h)
    nvec = (-h[1], h[0], 0.0)
    nm = norm(nvec)
    equatorial = nm < EQUATORIAL_SIN_I * hm
    circular = e < CIRCULAR_E
    if equatorial:
        O = 0.0
        inc = 0.0 if hh[2] > 0 else math.pi
        sgn = 1.0 if hh[2] > 0 else -1.0
        w = 0.0 if circular else math.atan2(sgn * ev[1], ev[0])
    else:
        inc = math.acos(max(-1.0, min(1.0, hh[2])))
        O = math.atan2(nvec[1], nvec[0])
        nh = tuple(c / nm for c in nvec)
        if circular:
            w = 0.0
        else:
            hxn = cross(hh, nh)
            w = math.atan2(dot(ev, hxn), dot(ev, nh))
    P = rot(O, inc, w, 1.0, 0.0)
    Q = rot(O, inc, w, 0.0, 1.0)
    nu = math.atan2(dot(r, Q), dot(r, P))
    E = 2 * math.atan2(math.sqrt(1 - e) * math.sin(nu / 2), math.sqrt(1 + e) * math.cos(nu / 2))
    M = E - e * math.sin(E)
    n = math.sqrt(MU / a**3)
    M0 = math.remainder(M - n * t0, 2 * math.pi)
    return (a, e, inc, O, w, M0)


cases = {
    "circular equatorial":        (RE + 400e3, 0.0, 0.0, 0.0, 0.0, 0.0),
    "circular inclined 32.5":     (RE + 185e3, 0.0, math.radians(32.5), 0.7, 0.0, 0.4),
    "elliptical inclined 51.6":   (12.0e6, 0.3, math.radians(51.6), 0.7, 1.1, 0.4),
    "elliptical equatorial":      (9.0e6, 0.1, 0.0, 0.0, 2.0, 1.0),
    "elliptical retro equatorial": (9.0e6, 0.1, math.pi, 0.0, 2.0, 1.0),
    "near-circular e=1e-4":       (RE + 300e3, 1e-4, math.radians(28.5), 1.2, 0.5, 2.0),
    "near-equatorial i=1e-4 deg": (RE + 300e3, 0.05, math.radians(1e-4), 0.3, 0.9, 2.5),
    "polar":                      (RE + 600e3, 0.02, math.radians(90), 2.0, 1.0, 3.0),
}

worst_pos = worst_vel = 0.0
for name, el in cases.items():
    wp = wv = 0.0
    for t0 in (0.0, 1234.5, 5000.0, 40000.0):
        r0, v0 = propagate(el, t0)
        new = from_state(r0, v0, t0)
        assert new is not None, name
        for dt in (0.0, 777.0, 20000.0, 86164.0):
            ra, va = propagate(el, t0 + dt)
            rb, vb = propagate(new, t0 + dt)
            wp = max(wp, norm(sub(ra, rb)))
            wv = max(wv, norm(sub(va, vb)))
    print(f"{name:30s} worst position error {wp:.2e} m, velocity {wv:.2e} m/s")
    worst_pos, worst_vel = max(worst_pos, wp), max(worst_vel, wv)
assert worst_pos < 1e-3 and worst_vel < 1e-6, (worst_pos, worst_vel)
print("ROUND TRIP OK (limits: 1e-3 m, 1e-6 m/s)")

# Burn tests from the default orbit: circular 185 km, i = 32.5 deg.
base = (RE + 185e3, 0.0, math.radians(32.5), 0.0, 0.0, 0.0)
r, v = propagate(base, 1000.0)
vh = tuple(c / norm(v) for c in v)
rm = norm(r)
print("\nImpulsive burns from the default orbit:")
for dv in (100.0, 10.0, -10.0, -30.0, -60.0):
    v2 = tuple(v[k] + dv * vh[k] for k in range(3))
    el = from_state(r, v2, 1000.0)
    a, e = el[0], el[1]
    peri, apo = a * (1 - e) - RE, a * (1 + e) - RE
    vv = norm(v2)
    a_expected = 1 / (2 / rm - vv * vv / MU)
    print(f"  dv {dv:+6.1f} m/s: periapsis {peri / 1000:8.2f} km, apoapsis {apo / 1000:8.2f} km (vis-viva a error {abs(a - a_expected):.2e} m)")
    assert abs(a - a_expected) < 1e-3
r100, v100 = r, tuple(v[k] + 100 * vh[k] for k in range(3))
el = from_state(r100, v100, 1000.0)
apo = el[0] * (1 + el[1]) - RE
assert abs(apo - 532_200) < 1500, apo
print("  +100 m/s apoapsis matches the vis-viva prediction (about 532 km)")

# Re-seeded orbit must conserve energy and angular momentum through the burn instant.
r1, v1 = propagate(el, 1000.0)
assert norm(sub(r1, r100)) < 1e-3 and norm(sub(v1, v100)) < 1e-6
print("  state is continuous across the re-seed instant")
print("ALL RESEED CHECKS PASSED")
