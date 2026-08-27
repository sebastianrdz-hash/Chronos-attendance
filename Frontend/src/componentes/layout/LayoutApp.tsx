import { MarcaChronos } from '@/componentes/MarcaChronos'
import { etiquetaRol, navegacionPara } from '@/auth/navegacion'
import { useAuth } from '@/auth/useAuth'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from '@/components/ui/sheet'
import { cn } from '@/lib/utils'
import { LogOut, Menu, UserRound } from 'lucide-react'
import { useState } from 'react'
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom'

function iniciales(nombre: string) {
  return nombre
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((parte) => parte[0]?.toUpperCase())
    .join('')
}

function Enlaces({ alNavegar }: { alNavegar?: () => void }) {
  const { usuario } = useAuth()
  if (!usuario) return null

  return (
    <nav className="flex flex-col gap-1">
      {navegacionPara(usuario.rol).map(({ ruta, etiqueta, icono: Icono }) => (
        <NavLink
          key={ruta}
          to={ruta}
          onClick={alNavegar}
          className={({ isActive }) =>
            cn(
              'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
              isActive
                ? 'bg-primary/10 text-primary'
                : 'text-muted-foreground hover:bg-muted hover:text-foreground',
            )
          }
        >
          <Icono className="size-4 shrink-0" />
          {etiqueta}
        </NavLink>
      ))}
    </nav>
  )
}

function Identidad() {
  const { usuario, cerrarSesion } = useAuth()
  const navegar = useNavigate()

  if (!usuario) return null

  return (
    <DropdownMenu>
      <DropdownMenuTrigger render={<Button variant="ghost" className="h-auto gap-2 px-2 py-1.5" />}>
        <Avatar className="size-8">
          <AvatarFallback className="bg-primary/10 text-xs font-semibold text-primary">
            {iniciales(usuario.nombre)}
          </AvatarFallback>
        </Avatar>
        <span className="hidden text-left sm:block">
          <span className="block text-sm font-medium leading-tight">{usuario.nombre}</span>
          <span className="block text-xs leading-tight text-muted-foreground">
            {usuario.puesto ?? usuario.correo}
          </span>
        </span>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-60">
        {/* Base UI exige que GroupLabel viva dentro de un Group; suelto revienta el menú. */}
        <DropdownMenuGroup>
          <DropdownMenuLabel className="font-normal">
            <p className="text-sm font-medium">{usuario.nombre}</p>
            <p className="text-xs text-muted-foreground">{usuario.correo}</p>
            {usuario.numeroEmpleado && (
              <p className="mt-1 text-xs text-muted-foreground">
                {usuario.numeroEmpleado} · {usuario.departamento}
              </p>
            )}
          </DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={() => navegar('/perfil')}>
          <UserRound className="size-4" />
          Mi perfil
        </DropdownMenuItem>
        <DropdownMenuItem
          variant="destructive"
          onClick={() => {
            cerrarSesion()
            navegar('/login', { replace: true })
          }}
        >
          <LogOut className="size-4" />
          Cerrar sesión
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

export function LayoutApp() {
  const { usuario } = useAuth()
  const [menuAbierto, setMenuAbierto] = useState(false)

  return (
    <div className="min-h-svh bg-muted/30">
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-60 flex-col border-r bg-background lg:flex">
        <div className="flex h-16 items-center border-b px-5">
          <Link to="/panel">
            <MarcaChronos />
          </Link>
        </div>
        <div className="flex-1 overflow-y-auto p-3">
          <Enlaces />
        </div>
        {usuario && (
          <div className="border-t p-4">
            <Badge variant="secondary" className="w-full justify-center">
              {etiquetaRol[usuario.rol]}
            </Badge>
          </div>
        )}
      </aside>

      <div className="lg:pl-60">
        <header className="sticky top-0 z-20 flex h-16 items-center justify-between gap-3 border-b bg-background/95 px-4 backdrop-blur sm:px-6">
          <div className="flex items-center gap-3">
            <Sheet open={menuAbierto} onOpenChange={setMenuAbierto}>
              <SheetTrigger
                render={
                  <Button variant="ghost" size="icon" className="lg:hidden" aria-label="Abrir menú" />
                }
              >
                <Menu className="size-5" />
              </SheetTrigger>
              <SheetContent side="left" className="w-64 p-0">
                <SheetTitle className="sr-only">Navegación</SheetTitle>
                <div className="flex h-16 items-center border-b px-5">
                  <MarcaChronos />
                </div>
                <div className="p-3">
                  <Enlaces alNavegar={() => setMenuAbierto(false)} />
                </div>
              </SheetContent>
            </Sheet>
            <Link to="/panel" className="lg:hidden">
              <MarcaChronos />
            </Link>
          </div>
          <Identidad />
        </header>

        <main className="mx-auto w-full max-w-7xl p-4 sm:p-6 lg:p-8">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
