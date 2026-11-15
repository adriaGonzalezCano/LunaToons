"""Python PORT of the V2 attitude core (RigidBodyAttitude): quaternion + body rates, Euler's equations, RK8 fixed step.
Uses the exact Rk8 coefficients shipped in Rk8Coefficients.cs. The C# itself is tested by Apollo > Run Simulation Checks."""
import math, re

src = open('/home/claude/apollo/Assets/Scripts/OrbitalSim/Rk8Coefficients.cs').read()
def arr(name):
    m = re.search(name + r"\s*=\s*\{\s*([^}]*)\}", src, re.S)
    return [float(x) for x in m.group(1).replace("\n", " ").split(",") if x.strip()]
C = arr("double\\[\\] C"); B = arr("double\\[\\] B")
A = [[float(x) for x in r.split(",") if x.strip()] for r in re.findall(r"new double\[\] \{([^}]*)\}", src)]

def rk8(f, t, y, h):
    k = []
    for i in range(12):
        yi = list(y)
        for j in range(i):
            if A[i][j]: yi = [a + A[i][j] * h * b for a, b in zip(yi, k[j])]
        k.append(f(t + C[i] * h, yi))
    out = list(y)
    for i in range(12):
        if B[i]: out = [a + B[i] * h * b for a, b in zip(out, k[i])]
    return out

def qmul(a, b):
    return (a[0]*b[0]-a[1]*b[1]-a[2]*b[2]-a[3]*b[3], a[0]*b[1]+a[1]*b[0]+a[2]*b[3]-a[3]*b[2],
            a[0]*b[2]-a[1]*b[3]+a[2]*b[0]+a[3]*b[1], a[0]*b[3]+a[1]*b[2]-a[2]*b[1]+a[3]*b[0])
def rotate(q, v):  # body -> inertial
    p = qmul(qmul(q, (0, *v)), (q[0], -q[1], -q[2], -q[3]))
    return p[1:]

STEP = 0.05
class Attitude:
    def __init__(self, inertia=(1.0, 1.0, 1.0), q=(1.0, 0, 0, 0), w=(0.0, 0, 0)):
        self.I = inertia; self.q = q; self.w = w; self.torque = (0.0, 0, 0)
    def f(self, t, y):
        q, w = y[:4], y[4:]
        I = self.I
        dq = qmul(q, (0, *w)); dq = [0.5 * x for x in dq]
        Iw = [I[i] * w[i] for i in range(3)]
        cr = (w[1]*Iw[2]-w[2]*Iw[1], w[2]*Iw[0]-w[0]*Iw[2], w[0]*Iw[1]-w[1]*Iw[0])
        dw = [(self.torque[i] - cr[i]) / I[i] for i in range(3)]
        return dq + dw
    def step(self, h):
        y = rk8(self.f, 0.0, list(self.q) + list(self.w), h)
        n = math.sqrt(sum(x*x for x in y[:4]))
        self.q = tuple(x / n for x in y[:4]); self.w = tuple(y[4:])
    def momentum_inertial(self):
        return rotate(self.q, [self.I[i] * self.w[i] for i in range(3)])
    def energy(self): return 0.5 * sum(self.I[i] * self.w[i]**2 for i in range(3))

def norm(v): return math.sqrt(sum(x*x for x in v))
def run(a, seconds, h=STEP):
    for _ in range(int(round(seconds / h))): a.step(h)

print("1. Torque-free, equal inertia: constant rate, exact rotation about the rate axis")
w0 = (0.1, -0.05, 0.2)
a = Attitude(w=w0); run(a, 120.0)
th = norm(w0) * 120.0; ax = [x / norm(w0) for x in w0]
qe = (math.cos(th/2), *[math.sin(th/2)*x for x in ax])
dot = abs(sum(x*y for x, y in zip(a.q, qe)))
print(f"   rate drift {norm([x-y for x,y in zip(a.w,w0)]):.1e} rad/s, quaternion difference {1-dot:.1e}")
assert norm([x-y for x,y in zip(a.w,w0)]) < 1e-12 and 1 - dot < 1e-12

print("2. Torque-free, unequal inertia (tumbling): L and energy conserved, |q| = 1")
a = Attitude(inertia=(1.0, 2.0, 3.0), q=(0.8, 0.2, -0.4, 0.4), w=(0.3, 0.02, -0.15))
n = norm(a.q); a.q = tuple(x/n for x in a.q)
L0, E0 = a.momentum_inertial(), a.energy()
run(a, 300.0)
dL = norm([x-y for x, y in zip(a.momentum_inertial(), L0)]) / norm(L0); dE = abs(a.energy()-E0)/E0
print(f"   dL/L {dL:.1e}, dE/E {dE:.1e}, |q|-1 {abs(norm(a.q)-1):.1e}")
assert dL < 1e-9 and dE < 1e-9 and abs(norm(a.q)-1) < 1e-12

print("3. Torque pulse: 2 deg/s^2 about body X for 5 s gives 10 deg/s and 25 deg")
alpha = math.radians(2.0)
a = Attitude(); a.torque = (alpha, 0, 0); run(a, 5.0); a.torque = (0, 0, 0)
ang = 2 * math.acos(a.q[0])
print(f"   rate {math.degrees(a.w[0]):.6f} deg/s, angle {math.degrees(ang):.6f} deg")
assert abs(math.degrees(a.w[0]) - 10) < 1e-9 and abs(math.degrees(ang) - 25) < 1e-6
run(a, 10.0)
print(f"   after release the rate persists: {math.degrees(a.w[0]):.6f} deg/s")
assert abs(math.degrees(a.w[0]) - 10) < 1e-9

print("4. Stop-rotation law (limited, proportional, smooth)")
MAX_ALPHA = math.radians(2.0); TAU = 1.0; SNAP = 1e-4
def stop_torque(w, I):
    out = []
    for i in range(3):
        u = max(-1.0, min(1.0, w[i] / (MAX_ALPHA * TAU)))
        out.append(-u * MAX_ALPHA * I[i])
    return out
a = Attitude(w=(math.radians(10), math.radians(-6), math.radians(3)))
t = 0.0; prev_sign = [1, -1, 1]; overshoot = False; peak_alpha = 0
while t < 120 and norm(a.w) > SNAP:
    tq = stop_torque(a.w, a.I); peak_alpha = max(peak_alpha, max(abs(x) for x in tq))
    a.torque = tuple(tq); a.step(STEP); t += STEP
    for i in range(3):
        if a.w[i] * prev_sign[i] < -1e-9: overshoot = True
print(f"   stopped after {t:.1f} s, residual rate {norm(a.w):.1e} rad/s, peak accel {math.degrees(peak_alpha):.3f} deg/s^2, overshoot: {overshoot}")
assert t < 20 and not overshoot and peak_alpha <= MAX_ALPHA * 1.0000001
print("ALL ATTITUDE CHECKS PASSED")
