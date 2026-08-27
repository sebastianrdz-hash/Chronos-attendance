import type { Empleado } from '@/api/tipos'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Check, Copy, TriangleAlert } from 'lucide-react'
import { useState } from 'react'

/**
 * La contraseña temporal no se guarda en claro ni se puede volver a consultar, así que
 * esta es la única oportunidad de copiarla. Si se pierde, queda reiniciar el acceso.
 */
export function CredencialTemporal({
  datos,
  onCerrar,
}: {
  datos: { empleado: Empleado; contrasena: string } | null
  onCerrar: () => void
}) {
  const [copiado, setCopiado] = useState(false)

  const copiar = async () => {
    if (!datos) return

    try {
      await navigator.clipboard.writeText(
        `Usuario: ${datos.empleado.correoCorporativo}\nContraseña temporal: ${datos.contrasena}`,
      )
      setCopiado(true)
      setTimeout(() => setCopiado(false), 2000)
    } catch {
      setCopiado(false)
    }
  }

  return (
    <Dialog
      open={datos !== null}
      onOpenChange={(valor) => {
        if (!valor) {
          setCopiado(false)
          onCerrar()
        }
      }}
    >
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Credenciales de acceso</DialogTitle>
          <DialogDescription>
            {datos?.empleado.nombreCompleto} ya puede entrar a Chronos.
          </DialogDescription>
        </DialogHeader>

        <Alert>
          <TriangleAlert className="size-4" />
          <AlertDescription>
            Esta contraseña no vuelve a mostrarse. Entrégala por un canal seguro; al primer
            ingreso el sistema obligará a cambiarla.
          </AlertDescription>
        </Alert>

        <dl className="space-y-3 rounded-lg border bg-muted/40 p-4">
          <div>
            <dt className="text-xs uppercase tracking-wide text-muted-foreground">Usuario</dt>
            <dd className="font-mono text-sm">{datos?.empleado.correoCorporativo}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-wide text-muted-foreground">
              Contraseña temporal
            </dt>
            <dd className="font-mono text-lg font-semibold tracking-wide">{datos?.contrasena}</dd>
          </div>
        </dl>

        <DialogFooter>
          <Button variant="outline" onClick={copiar}>
            {copiado ? <Check className="size-4" /> : <Copy className="size-4" />}
            {copiado ? 'Copiado' : 'Copiar credenciales'}
          </Button>
          <Button onClick={onCerrar}>Entendido</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
