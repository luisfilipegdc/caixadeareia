"""
Calculadora de geometria da montagem física.

Responde: a que altura o Kinect precisa estar para enxergar a caixa inteira,
quanto ruído esperar nessa distância, e que throw ratio o projetor precisa ter.

Ajuste as constantes abaixo e rode:  python docs/geometria.py

Resultados desta configuração estão documentados em docs/MONTAGEM-FISICA.md.
"""

import math

# --- Kinect v1: campo de visão nominal, em graus ---
# O eixo de 57 graus corresponde aos 640 pixels; o de 43, aos 480.
# Near mode não altera o campo de visão, apenas a faixa de distância válida.
FOV_H, FOV_V = 57.0, 43.0

# Fatores de cobertura: área enxergada = fator x distância
kh = 2 * math.tan(math.radians(FOV_H / 2))
kv = 2 * math.tan(math.radians(FOV_V / 2))

# --- Caixa, em centímetros (medidas externas) ---
CAIXA_L, CAIXA_C = 101.0, 125.0

# --- Pórtico em U invertido, altura total do chão ---
PORTICO_H = 179.0

# Alturas de viga a comparar (quanto o Kinect desce abaixo do topo)
VIGAS = [0, 15, 25, 40]

print("Fatores de cobertura: horizontal %.4f*d   vertical %.4f*d" % (kh, kv))
print("Aspecto do FOV: %.3f   |   aspecto da caixa: %.3f" % (kh / kv, CAIXA_C / CAIXA_L))
print()

# O eixo estreito do sensor é quem limita, porque a caixa é mais "quadrada"
# que o campo de visão.
d_comp = CAIXA_C / kh
d_larg = CAIXA_L / kv
d_min = max(d_comp, d_larg)

print("DISTANCIA MINIMA DO KINECT A AREIA")
print("  cobrir %.0f cm no eixo 57 graus: %6.1f cm" % (CAIXA_C, d_comp))
print("  cobrir %.0f cm no eixo 43 graus: %6.1f cm   <-- LIMITANTE" % (CAIXA_L, d_larg))
print("  necessario: %.1f cm  |  com 8%% de margem: %.1f cm" % (d_min, d_min * 1.08))
print()

# Montar o sensor girado 90 graus troca qual eixo cobre qual dimensão.
d_girado = max(CAIXA_L / kh, CAIXA_C / kv)
print("  se montado girado 90 graus, precisaria de %.1f cm (+%.1f cm)"
      % (d_girado, d_girado - d_min))
print()

print("COBERTURA POR DISTANCIA")
print("   dist        cobre (C x L)      cabe?   mm/px   ruido")
for d in [110, 120, 128, 135, 139, 145, 150, 159, 165]:
    w, h = kh * d, kv * d
    ok = "sim" if (w >= CAIXA_C and h >= CAIXA_L) else "NAO"
    # O ruído do Kinect v1 cresce aproximadamente com o quadrado da distância.
    ruido = 1.5 * (d / 100.0) ** 2
    print("  %4d cm  %6.1f x %5.1f cm   %5s  %5.2f  %5.1f mm"
          % (d, w, h, ok, w * 10 / 640, ruido))
print()

print("DISTANCIA REAL ATE A AREIA (portico de %d cm)" % PORTICO_H)
print("  rodinha  areia  sup.  |" + "".join("  viga %2dcm  " % v for v in VIGAS))
for rod in [8, 12, 15]:
    for areia in [10, 12]:
        sup = rod + areia
        linha = "   %2d cm   %2d cm  %2d cm |" % (rod, areia, sup)
        for viga in VIGAS:
            d = (PORTICO_H - viga) - sup
            linha += " %5.1f %-5s|" % (d, "OK" if d >= d_min else "CURTO")
        print(linha)
print()
print("  (OK = cobre a caixa inteira; precisa de >= %.1f cm)" % d_min)
print()

print("PROJETOR - throw ratio necessario (distancia / largura da imagem)")
for aspecto, nome in [(4 / 3.0, "4:3"), (16 / 9.0, "16:9"), (16 / 10.0, "16:10")]:
    # O projetor precisa cobrir o lado curto da caixa; a largura resultante
    # depende do formato da imagem.
    larg_img = max(CAIXA_C, CAIXA_L * aspecto)
    print("  %-6s precisa de imagem com %.1f cm de largura" % (nome, larg_img))
    for viga in [0, 25]:
        d = (PORTICO_H - viga) - 22   # 22 cm = rodinha 12 + areia 10
        print("       a %5.1f cm de altura util -> throw ratio %.2f" % (d, d / larg_img))
