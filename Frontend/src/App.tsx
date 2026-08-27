import { ProveedorAuth } from '@/auth/ProveedorAuth'
import { RutaProtegida } from '@/auth/RutaProtegida'
import { LayoutApp } from '@/componentes/layout/LayoutApp'
import { Toaster } from '@/components/ui/sonner'
import { CambioObligatorio } from '@/paginas/CambioObligatorio'
import { Departamentos } from '@/paginas/catalogos/Departamentos'
import { Sedes } from '@/paginas/catalogos/Sedes'
import { Turnos } from '@/paginas/catalogos/Turnos'
import { Empleados } from '@/paginas/empleados/Empleados'
import { PaginaLogin } from '@/paginas/Login'
import { MiPerfil } from '@/paginas/MiPerfil'
import { Panel } from '@/paginas/Panel'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'

export default function App() {
  return (
    <BrowserRouter>
      <ProveedorAuth>
        <Routes>
          <Route path="/login" element={<PaginaLogin />} />

          {/* Fuera del layout: mientras haya contraseña temporal no hay navegación que ofrecer. */}
          <Route element={<RutaProtegida />}>
            <Route path="/cambiar-contrasena" element={<CambioObligatorio />} />
          </Route>

          <Route element={<RutaProtegida />}>
            <Route element={<LayoutApp />}>
              <Route path="/panel" element={<Panel />} />
              <Route path="/perfil" element={<MiPerfil />} />
            </Route>
          </Route>

          <Route element={<RutaProtegida roles={['Admin', 'Supervisor']} />}>
            <Route element={<LayoutApp />}>
              <Route path="/empleados" element={<Empleados />} />
              <Route path="/departamentos" element={<Departamentos />} />
              <Route path="/sedes" element={<Sedes />} />
              <Route path="/turnos" element={<Turnos />} />
            </Route>
          </Route>

          <Route path="*" element={<Navigate to="/panel" replace />} />
        </Routes>

        <Toaster richColors position="top-right" />
      </ProveedorAuth>
    </BrowserRouter>
  )
}
