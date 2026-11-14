"""Cross-check of the KeplerOrbit math. This is a Python PORT of KeplerOrbit.cs, not the C# itself.
It validates the algorithm; the C# still has to be run in Unity to count as tested."""
import math

MU = 3.986004418e14
RE = 6_371_000.0


def solve_kepler(m, e):
    ecc = m if e < 0.8 else math.pi
    for _ in range(50):
        d = (ecc - e * math.sin(ecc) - m) / (1 - e * math.cos(ecc))
        ecc -= d
        if abs(d) < 1e-12:
            break
    return ecc


def propagate(a, e, inc, raan, argp, m0, t):
    n = math.sqrt(MU / a**3)
    m = math.remainder(m0 + n * t, 2 * math.pi)
    E = solve_kepler(m, e)
    cE, sE = math.cos(E), math.sin(E)
    r = a * (1 - e * cE)
    xp, yp = a * (cE - e), a * math.sqrt(1 - e * e) * sE
    vs = math.sqrt(MU * a) / r
    vxp, vyp = -vs * sE, vs * math.sqrt(1 - e * e) * cE
    cO, sO, cw, sw, ci, si = math.cos(raan), math.sin(raan), math.cos(argp), math.sin(argp), math.cos(inc), math.sin(inc)

    def rot(x, y):
        return ((cO * cw - sO * sw * ci) * x + (-cO * sw - sO * cw * ci) * y,
                (sO * cw + cO * sw * ci) * x + (-sO * sw + cO * cw * ci) * y,
                (sw * si) * x + (cw * si) * y)
    return rot(xp, yp), rot(vxp, vyp), 2 * math.pi / n


def norm(v):
    return math.sqrt(sum(c * c for c in v))


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def check(name, a, e, inc_deg):
    inc = math.radians(inc_deg)
    args = (a, e, inc, 0.7, 1.1, 0.4)
    _, _, period = propagate(*args, 0.0)
    eps0 = None
    h0 = None
    worst_e = worst_h = 0.0
    for k in range(0, 2001):
        t = period * 3 * k / 2000
        r, v, _ = propagate(*args, t)
        eps = norm(v) ** 2 / 2 - MU / norm(r)
        h = norm(cross(r, v))
        eps0 = eps if eps0 is None else eps0
        h0 = h if h0 is None else h0
        worst_e = max(worst_e, abs(eps - eps0) / abs(eps0))
        worst_h = max(worst_h, abs(h - h0) / h0)
    # Period vs Kepler's third law, periodicity, vis-viva
    r0, v0, _ = propagate(*args, 0.0)
    r1, v1, _ = propagate(*args, period)
    pos_err = norm(tuple(x - y for x, y in zip(r0, r1)))
    vis_viva = math.sqrt(MU * (2 / norm(r0) - 1 / a))
    print(f"{name}: period={period:.1f}s  energy drift={worst_e:.2e}  ang.mom drift={worst_h:.2e}  "
          f"pos error after 1 period={pos_err:.3e} m  vis-viva speed err={abs(vis_viva - norm(v0)):.2e} m/s")
    assert worst_e < 1e-9 and worst_h < 1e-9 and pos_err < 1e-3 and abs(vis_viva - norm(v0)) < 1e-6
    return period


p = check("circular 185 km, 32.5 deg", RE + 185_000, 0.0, 32.5)
print(f"  period check: {p / 60:.2f} min (expected ~88 min for 185 km)")
r, v, _ = propagate(RE + 185_000, 0.0, math.radians(32.5), 0, 0, 0, 1234.5)
print(f"  altitude constant: {(norm(r) - RE) / 1000:.6f} km, speed {norm(v):.2f} m/s (expected sqrt(mu/r)={math.sqrt(MU / (RE + 185000)):.2f})")
check("elliptical e=0.3, a=12000 km, 51.6 deg", 12_000_000.0, 0.3, 51.6)
check("circular equatorial", RE + 400_000, 0.0, 0.0)
print("ALL CHECKS PASSED")
