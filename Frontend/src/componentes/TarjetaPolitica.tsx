import { api } from '@/api/cliente'
import type { PoliticaConfianza } from '@/api/tipos'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useEffect, useState } from 'react'

const PESO_MAXIMO = 45

export function TarjetaPolitica() {
  const [politica, setPolitica] = useState<PoliticaConfianza | null>(null)

  useEffect(() => {
    api
      .politicaConfianza()
      .then(setPolitica)
      .catch(() => setPolitica(null))
  }, [])

  return (
    <Card>
      <CardHeader>
        <CardTitle>Política de confianza</CardTitle>
        <CardDescription>
          Cuánto vale cada señal y cuándo una checada se da por buena. Con{' '}
          {politica?.umbralAlta ?? 70} puntos queda verificada; por debajo de{' '}
          {politica?.umbralMedia ?? 40} se marca para revisión.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {!politica ? (
          <div className="space-y-3">
            {Array.from({ length: 4 }, (_, i) => (
              <Skeleton key={i} className="h-12 w-full" />
            ))}
          </div>
        ) : (
          <ul className="space-y-3">
            {politica.senales.map((senal) => (
              <li key={senal.tipo} className="space-y-1.5">
                <div className="flex items-center justify-between gap-3">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium">{senal.tipo}</span>
                    {!senal.disponibleEnFase1 && (
                      <Badge variant="outline" className="text-[10px]">
                        fase 2
                      </Badge>
                    )}
                  </div>
                  <span className="font-mono text-sm tabular-nums text-muted-foreground">
                    {senal.peso} pts
                  </span>
                </div>
                <div className="h-1.5 overflow-hidden rounded-full bg-muted" role="presentation">
                  <div
                    className={
                      senal.disponibleEnFase1
                        ? 'h-full rounded-full bg-primary'
                        : 'h-full rounded-full bg-muted-foreground/40'
                    }
                    style={{ width: `${(senal.peso / PESO_MAXIMO) * 100}%` }}
                  />
                </div>
                <p className="text-xs text-muted-foreground">{senal.prueba}</p>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}
